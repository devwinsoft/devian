//#define DEVIAN_NET_DEBUG  // Uncomment locally for debug logs (default OFF for zero GC)
#nullable enable
using System;
using System.Buffers;
using System.Collections.Generic;

#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#else
using System.Net.WebSockets;
using System.Threading;
#endif

namespace Devian
{
    /// <summary>
    /// WebSocket network client with sync public API.
    ///
    /// Platform behavior:
    /// - Editor/Standalone: background threads with ClientWebSocket
    /// - WebGL: browser WebSocket via JS interop (polling-based), all on main thread
    ///
    /// Events are dispatched via Tick() for Unity main thread compatibility.
    /// Update() is an alias for Tick() (legacy compatibility).
    /// </summary>
    public sealed class NetWsClient : INetTickable, IDisposable
    {
        private readonly NetClient _core;
        private readonly int _sessionId;

        private volatile bool _running;

        // Dispatch queue for Unity main thread (shared by all platforms)
        private readonly object _dispatchLock = new();
        private readonly Queue<Action> _dispatchQueue = new();

#if UNITY_WEBGL && !UNITY_EDITOR
        // ========== WebGL Implementation (Polling-based) ==========

        // --- DllImport declarations ---
        [DllImport("__Internal")]
        private static extern int WS_Connect(string url, string subProtocolsJson);

        [DllImport("__Internal")]
        private static extern int WS_GetState(int socketId);

        [DllImport("__Internal")]
        private static extern int WS_SendBinary(int socketId, IntPtr ptr, int len);

        [DllImport("__Internal")]
        private static extern void WS_Close(int socketId, int code, string reason);

        // Polling
        [DllImport("__Internal")]
        private static extern int WS_PollEvent(
            int socketId,
            out int eventType,
            out int code,
            out IntPtr dataPtr,
            out int dataLen,
            out IntPtr messagePtr
        );

        // Memory
        [DllImport("__Internal")]
        private static extern void WS_FreeBuffer(IntPtr ptr);

        [DllImport("__Internal")]
        private static extern void WS_FreeString(IntPtr ptr);

        // --- Event type constants (must match jslib) ---
        private const int EVT_OPEN = 1;
        private const int EVT_CLOSE = 2;
        private const int EVT_ERROR = 3;
        private const int EVT_MESSAGE = 4;

        // --- Tick cap constants (prevents GC/memory explosion during event flooding) ---
        private const int MaxEventsPerTick = 128;
        private const int MaxBytesPerTick = 256 * 1024; // 256KB

        private int _socketId = -1;

        /// <summary>
        /// Fired when connection is established.
        /// </summary>
        public event Action? OnOpen;

        /// <summary>
        /// Fired when connection is closed.
        /// </summary>
        public event Action<ushort, string>? OnClose;

        /// <summary>
        /// Fired when an error occurs.
        /// </summary>
        public event Action<Exception>? OnError;

        /// <summary>
        /// Creates a new NetWsClient.
        /// </summary>
        /// <param name="core">The core that handles frame dispatch.</param>
        /// <param name="sessionId">Session identifier for this connection.</param>
        public NetWsClient(NetClient core, int sessionId = 0)
        {
            _core = core ?? throw new ArgumentNullException(nameof(core));
            _sessionId = sessionId;
        }

        /// <summary>
        /// Returns true if the WebSocket is open.
        /// </summary>
        public bool IsOpen => _socketId >= 0 && WS_GetState(_socketId) == 1; // 1 = OPEN

        /// <summary>
        /// Connect to WebSocket server (WebGL: async via browser).
        /// OnOpen will be dispatched via Tick() when connection is established.
        /// </summary>
        /// <param name="url">WebSocket URL (wss:// required for WebGL).</param>
        /// <param name="subProtocols">Optional sub-protocols.</param>
        public void Connect(string url, string[]? subProtocols = null)
        {
            if (_running)
                throw new InvalidOperationException("Already running.");

            _running = true;

            // Convert subProtocols to JSON
            var subProtocolsJson = "";
            if (subProtocols != null && subProtocols.Length > 0)
            {
                subProtocolsJson = "[\"" + string.Join("\",\"", subProtocols) + "\"]";
            }

            // Call JS to connect (polling-based: no driver registration)
            _socketId = WS_Connect(url, subProtocolsJson);
            // OnOpen will be received via WS_PollEvent in Tick()
        }

        /// <summary>
        /// Request graceful close.
        /// </summary>
        public void Close()
        {
            if (!_running || _socketId < 0) return;

            WS_Close(_socketId, 1000, "Normal Closure");
            // OnClose will be received via WS_PollEvent in Tick()
        }

        /// <summary>
        /// Send a frame (WebGL: immediate send via JS, ptr+len).
        /// </summary>
        /// <param name="frame">Frame bytes to send.</param>
        public void SendFrame(ReadOnlySpan<byte> frame)
        {
            if (!_running || _socketId < 0) return;
            if (WS_GetState(_socketId) != 1) return; // Not OPEN

            var len = frame.Length;
            if (len == 0) return;

            // Rent buffer, copy, pin, send, unpin, return (no ToArray, no Base64)
            var rented = ArrayPool<byte>.Shared.Rent(len);
            var handle = default(GCHandle);
            try
            {
                frame.CopyTo(rented.AsSpan(0, len));
                handle = GCHandle.Alloc(rented, GCHandleType.Pinned);
                var ptr = handle.AddrOfPinnedObject();
                WS_SendBinary(_socketId, ptr, len);
            }
            finally
            {
                if (handle.IsAllocated) handle.Free();
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        /// <summary>
        /// Standard pump function: polls JS events and drains dispatch queue.
        /// Call this from Unity's Update() for main thread event handling.
        /// Applies MaxEventsPerTick and MaxBytesPerTick caps to prevent GC spike.
        /// </summary>
        public void Tick()
        {
            if (!_running || _socketId < 0)
            {
                DrainDispatchQueue();
                return;
            }

            // 1) Poll events from JS (with cap)
            var processedEvents = 0;
            var processedBytes = 0;

            while (processedEvents < MaxEventsPerTick && processedBytes < MaxBytesPerTick)
            {
                int eventType, code, dataLen;
                IntPtr dataPtr, messagePtr;

                var has = WS_PollEvent(_socketId, out eventType, out code, out dataPtr, out dataLen, out messagePtr);
                if (has == 0) break;

                processedEvents++;

                switch (eventType)
                {
                    case EVT_OPEN:
                        SafeDispatch(() => OnOpen?.Invoke());
                        break;

                    case EVT_CLOSE:
                        {
                            var reason = ReadAndFreeString(messagePtr);
                            var closeCode = (ushort)code;
                            SafeDispatch(() => OnClose?.Invoke(closeCode, reason));
                            // Local state cleanup (socketId invalid after close)
                            _running = false;
                            _socketId = -1;
                        }
                        break;

                    case EVT_ERROR:
                        {
                            var msg = ReadAndFreeString(messagePtr);
                            SafeDispatch(() => OnError?.Invoke(new Exception(msg)));
                        }
                        break;

                    case EVT_MESSAGE:
                        processedBytes += dataLen;
                        HandlePolledMessage(dataPtr, dataLen);
                        break;
                }
            }

#if DEVIAN_NET_DEBUG
            if (processedEvents >= MaxEventsPerTick || processedBytes >= MaxBytesPerTick)
                UnityEngine.Debug.Log("[NetWsClient] Tick cap reached");
#endif

            // 2) Drain dispatch queue
            DrainDispatchQueue();
        }

        /// <summary>
        /// Legacy alias for Tick(). Prefer Tick() for new code.
        /// </summary>
        public void Update() => Tick();

        /// <inheritdoc />
        public void Dispose()
        {
            if (_socketId >= 0)
            {
                try { Close(); } catch { /* ignore */ }
                _socketId = -1;
            }
            _running = false;
        }

        // ========== Internal helpers ==========

        private void SafeDispatch(Action action)
        {
            lock (_dispatchLock)
            {
                _dispatchQueue.Enqueue(action);
            }
        }

        private void DrainDispatchQueue()
        {
            while (true)
            {
                Action? action;
                lock (_dispatchLock)
                {
                    if (_dispatchQueue.Count == 0) return;
                    action = _dispatchQueue.Dequeue();
                }

                try
                {
                    action();
                }
                catch
                {
                    // Swallow user handler exceptions
                }
            }
        }

        private string ReadAndFreeString(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero) return "";
            var str = Marshal.PtrToStringUTF8(ptr) ?? "";
            WS_FreeString(ptr);
            return str;
        }

        private void HandlePolledMessage(IntPtr dataPtr, int dataLen)
        {
            if (dataPtr == IntPtr.Zero || dataLen <= 0)
            {
                if (dataPtr != IntPtr.Zero) WS_FreeBuffer(dataPtr);
                return;
            }

            byte[]? rented = null;
            try
            {
                rented = ArrayPool<byte>.Shared.Rent(dataLen);
                Marshal.Copy(dataPtr, rented, 0, dataLen);

                // Pass to core (Span-based, no storage)
                _core.OnFrame(_sessionId, rented.AsSpan(0, dataLen));
            }
            finally
            {
                // Free JS malloc buffer
                WS_FreeBuffer(dataPtr);
                if (rented != null) ArrayPool<byte>.Shared.Return(rented);
            }
        }

#else
        // ========== Editor/Standalone Implementation ==========

        // --- Tick cap constants (prevents GC/memory explosion during event flooding) ---
        private const int MaxEventsPerTick = 128;
        private const int MaxBytesPerTick = 256 * 1024; // 256KB

        private ClientWebSocket? _ws;
        private Thread? _recvThread;
        private Thread? _sendThread;
        private CancellationTokenSource? _cts;
        private volatile bool _closeRequested;

        // Send queue (sync enqueue from main thread)
        private readonly object _sendLock = new();
        private readonly Queue<SendItem> _sendQueue = new();

        // Receive buffer (pooled, reused)
        private byte[]? _recvBuffer;
        private int _recvLen;

        /// <summary>
        /// Fired when connection is established.
        /// </summary>
        public event Action? OnOpen;

        /// <summary>
        /// Fired when connection is closed.
        /// </summary>
        public event Action<ushort, string>? OnClose;

        /// <summary>
        /// Fired when an error occurs.
        /// </summary>
        public event Action<Exception>? OnError;

        /// <summary>
        /// Creates a new NetWsClient.
        /// </summary>
        /// <param name="core">The core that handles frame dispatch.</param>
        /// <param name="sessionId">Session identifier for this connection.</param>
        public NetWsClient(NetClient core, int sessionId = 0)
        {
            _core = core ?? throw new ArgumentNullException(nameof(core));
            _sessionId = sessionId;
        }

        /// <summary>
        /// Returns true if the WebSocket is open.
        /// </summary>
        public bool IsOpen => _ws != null && _ws.State == WebSocketState.Open;

        /// <summary>
        /// Synchronously connect and start background recv/send threads.
        /// </summary>
        /// <param name="url">WebSocket URL (ws:// or wss://).</param>
        /// <param name="subProtocols">Optional sub-protocols.</param>
        public void Connect(string url, string[]? subProtocols = null)
        {
            if (_running)
                throw new InvalidOperationException("Already running.");

            // Set running before connect attempt so dispatch queue events
            // from a failed ConnectAsync are guaranteed to be processed by Tick().
            // CleanupSocket() will reset _running = false on failure.
            _running = true;

            _ws = new ClientWebSocket();
            if (subProtocols != null)
            {
                foreach (var p in subProtocols)
                {
                    if (!string.IsNullOrWhiteSpace(p))
                        _ws.Options.AddSubProtocol(p);
                }
            }

            try
            {
                _ws.ConnectAsync(new Uri(url), CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                SafeDispatch(() => OnError?.Invoke(ex));
                SafeDispatch(() => OnClose?.Invoke(1006, ex.Message)); // 1006 = Abnormal Closure
                CleanupSocket();
                return;
            }

            // Clean up previous CTS and initialize new one
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            _closeRequested = false;

            // Start send thread
            _sendThread = new Thread(SendLoop) { IsBackground = true, Name = "NetWsClient.Send" };
            _sendThread.Start();

            // Start recv thread
            _recvThread = new Thread(RecvLoop) { IsBackground = true, Name = "NetWsClient.Recv" };
            _recvThread.Start();

            SafeDispatch(() => OnOpen?.Invoke());
        }

        /// <summary>
        /// Request graceful close.
        /// </summary>
        public void Close()
        {
            if (!_running) return;
            _closeRequested = true;
            EnqueueClose();
            _cts?.Cancel(); // Cancel RecvLoop to ensure OnClose fires
        }

        /// <summary>
        /// Synchronously enqueue a frame for sending.
        /// The frame is copied to a pooled buffer (no GC allocation on hot path).
        /// </summary>
        /// <param name="frame">Frame bytes to send.</param>
        public void SendFrame(ReadOnlySpan<byte> frame)
        {
            if (!_running || _ws == null) return;
            if (_ws.State != WebSocketState.Open) return;

            // Rent buffer and copy frame (no ToArray)
            var rented = ArrayPool<byte>.Shared.Rent(frame.Length);
            frame.CopyTo(rented.AsSpan(0, frame.Length));

            lock (_sendLock)
            {
                _sendQueue.Enqueue(SendItem.Binary(rented, frame.Length));
                Monitor.Pulse(_sendLock);
            }
        }

        /// <summary>
        /// Standard pump function: drains the dispatch queue.
        /// Call this from Unity's Update() for main thread event handling.
        /// Applies MaxEventsPerTick cap to prevent GC spike.
        /// </summary>
        public void Tick()
        {
            var processedEvents = 0;

            while (processedEvents < MaxEventsPerTick)
            {
                Action? action;
                lock (_dispatchLock)
                {
                    if (_dispatchQueue.Count == 0) return;
                    action = _dispatchQueue.Dequeue();
                }

                processedEvents++;

                try
                {
                    action();
                }
                catch
                {
                    // Swallow user handler exceptions
                }
            }

#if DEVIAN_NET_DEBUG
            if (processedEvents >= MaxEventsPerTick)
                UnityEngine.Debug.Log("[NetWsClient] Tick cap reached");
#endif
        }

        /// <summary>
        /// Legacy alias for Tick(). Prefer Tick() for new code.
        /// </summary>
        public void Update() => Tick();

        /// <inheritdoc />
        public void Dispose()
        {
            try { Close(); } catch { /* ignore */ }
            _running = false;

            // Best-effort thread join
            try { _sendThread?.Join(100); } catch { }
            try { _recvThread?.Join(100); } catch { }

            CleanupSocket();
        }

        private void SafeDispatch(Action action)
        {
            lock (_dispatchLock)
            {
                _dispatchQueue.Enqueue(action);
            }
        }

        private void EnqueueClose()
        {
            lock (_sendLock)
            {
                // Clear pending sends and enqueue close
                ClearSendQueueNoThrow();
                _sendQueue.Enqueue(SendItem.CloseItem());
                Monitor.Pulse(_sendLock);
            }
        }

        private void SendLoop()
        {
            try
            {
                while (_running && _ws != null)
                {
                    SendItem item;

                    lock (_sendLock)
                    {
                        while (_running && _sendQueue.Count == 0)
                            Monitor.Wait(_sendLock);

                        if (!_running || _ws == null) break;

                        item = _sendQueue.Dequeue();
                    }

                    var token = _cts?.Token ?? CancellationToken.None;

                    if (item.Type == SendItemType.Close)
                    {
                        try
                        {
                            _ws.CloseOutputAsync(
                                WebSocketCloseStatus.NormalClosure,
                                "Normal Closure",
                                token).GetAwaiter().GetResult();
                        }
                        catch (OperationCanceledException) when (_closeRequested)
                        {
                            // Expected during local close
                        }
                        catch (Exception ex)
                        {
                            if (!_closeRequested)
                                SafeDispatch(() => OnError?.Invoke(ex));
                        }
                        continue;
                    }

                    if (_ws.State != WebSocketState.Open)
                    {
                        item.Dispose();
                        continue;
                    }

                    try
                    {
                        _ws.SendAsync(
                            new ArraySegment<byte>(item.Buffer!, 0, item.Length),
                            WebSocketMessageType.Binary,
                            endOfMessage: true,
                            cancellationToken: token).GetAwaiter().GetResult();
                    }
                    catch (OperationCanceledException) when (_closeRequested)
                    {
                        // Expected during local close
                    }
                    catch (Exception ex)
                    {
                        if (!_closeRequested)
                            SafeDispatch(() => OnError?.Invoke(ex));
                    }
                    finally
                    {
                        item.Dispose(); // Return buffer to pool
                    }
                }
            }
            finally
            {
                lock (_sendLock)
                {
                    ClearSendQueueNoThrow();
                }
            }
        }

        private void RecvLoop()
        {
            ushort closeCode = 0;
            var closeReason = "";

            try
            {
                if (_ws == null || _cts == null) return;
                var token = _cts.Token;

                // Start with 64KB buffer, grow as needed
                _recvBuffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
                _recvLen = 0;

                while (_running && _ws.State == WebSocketState.Open)
                {
                    EnsureRecvCapacity(minFree: 8 * 1024);

                    var segment = new ArraySegment<byte>(_recvBuffer!, _recvLen, _recvBuffer!.Length - _recvLen);
                    var result = _ws.ReceiveAsync(segment, token).GetAwaiter().GetResult();

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        closeCode = (ushort)(result.CloseStatus ?? WebSocketCloseStatus.NormalClosure);
                        closeReason = result.CloseStatusDescription ?? "";
                        break;
                    }

                    _recvLen += result.Count;

                    if (!result.EndOfMessage)
                        continue;

                    // Frame complete: pass to core (zero-copy span)
                    var frameSpan = _recvBuffer.AsSpan(0, _recvLen);
                    _core.OnFrame(_sessionId, frameSpan);

                    // Reset for next message (buffer is reused)
                    _recvLen = 0;
                }
            }
            catch (OperationCanceledException) when (_closeRequested)
            {
                // Close() called - normal local close
                closeCode = 1000; // Normal Closure
                closeReason = "Local Close";
            }
            catch (Exception ex)
            {
                // Don't fire OnError if this was a requested close
                if (!_closeRequested)
                {
                    SafeDispatch(() => OnError?.Invoke(ex));
                }
                closeCode = _closeRequested ? (ushort)1000 : (ushort)1006;
                closeReason = _closeRequested ? "Local Close" : ex.Message;
            }
            finally
            {
                _running = false;
                SafeDispatch(() => OnClose?.Invoke(closeCode, closeReason));
                CleanupSocket();
            }
        }

        private void EnsureRecvCapacity(int minFree)
        {
            if (_recvBuffer == null) return;

            var free = _recvBuffer.Length - _recvLen;
            if (free >= minFree) return;

            // Grow buffer: double until enough
            var newSize = _recvBuffer.Length;
            while ((newSize - _recvLen) < minFree)
                newSize *= 2;

            var newBuf = ArrayPool<byte>.Shared.Rent(newSize);
            _recvBuffer.AsSpan(0, _recvLen).CopyTo(newBuf.AsSpan(0, _recvLen));

            ArrayPool<byte>.Shared.Return(_recvBuffer);
            _recvBuffer = newBuf;
        }

        private void CleanupSocket()
        {
            _running = false;

            try { _cts?.Dispose(); } catch { /* ignore */ }
            _cts = null;

            try { _ws?.Dispose(); } catch { /* ignore */ }
            _ws = null;

            if (_recvBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(_recvBuffer);
                _recvBuffer = null;
            }
            _recvLen = 0;
        }

        private void ClearSendQueueNoThrow()
        {
            while (_sendQueue.Count > 0)
            {
                var item = _sendQueue.Dequeue();
                item.Dispose();
            }
        }

        private enum SendItemType { Binary, Close }

        private readonly struct SendItem : IDisposable
        {
            public readonly SendItemType Type;
            public readonly byte[]? Buffer;
            public readonly int Length;

            private SendItem(SendItemType type, byte[]? buffer, int length)
            {
                Type = type;
                Buffer = buffer;
                Length = length;
            }

            public static SendItem Binary(byte[] rentedBuffer, int length) =>
                new SendItem(SendItemType.Binary, rentedBuffer, length);

            public static SendItem CloseItem() =>
                new SendItem(SendItemType.Close, null, 0);

            public void Dispose()
            {
                if (Type == SendItemType.Binary && Buffer != null)
                    ArrayPool<byte>.Shared.Return(Buffer);
            }
        }
#endif
    }
}
