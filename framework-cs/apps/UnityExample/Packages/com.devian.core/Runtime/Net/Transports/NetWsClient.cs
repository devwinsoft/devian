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
    /// - WebGL: browser WebSocket via JS interop, all on main thread
    /// 
    /// Events are dispatched via Update() for Unity main thread compatibility.
    /// </summary>
    public sealed class NetWsClient : IDisposable
    {
        private readonly NetClient _core;
        private readonly int _sessionId;

        private volatile bool _running;

        // Dispatch queue for Unity main thread (shared by all platforms)
        private readonly object _dispatchLock = new();
        private readonly Queue<Action> _dispatchQueue = new();

#if UNITY_WEBGL && !UNITY_EDITOR
        // ========== WebGL Implementation ==========

        [DllImport("__Internal")]
        private static extern int WS_Connect(string url, string subProtocolsJson);

        [DllImport("__Internal")]
        private static extern int WS_GetState(int socketId);

        [DllImport("__Internal")]
        private static extern int WS_SendBinary(int socketId, IntPtr ptr, int len);

        [DllImport("__Internal")]
        private static extern void WS_Close(int socketId, int code, string reason);

        [DllImport("__Internal")]
        private static extern void WS_FreeBuffer(int ptr);

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
        /// </summary>
        /// <param name="url">WebSocket URL (wss:// required for WebGL).</param>
        /// <param name="subProtocols">Optional sub-protocols.</param>
        public void Connect(string url, string[]? subProtocols = null)
        {
            if (_running)
                throw new InvalidOperationException("Already running.");

            _running = true;

            // Ensure driver singleton exists
            var driver = WebGLWsDriver.Instance;

            // Convert subProtocols to JSON
            var subProtocolsJson = "";
            if (subProtocols != null && subProtocols.Length > 0)
            {
                subProtocolsJson = "[\"" + string.Join("\",\"", subProtocols) + "\"]";
            }

            // Call JS to connect
            _socketId = WS_Connect(url, subProtocolsJson);

            // Register with driver for callbacks
            driver.Register(_socketId, this);
        }

        /// <summary>
        /// Request graceful close.
        /// </summary>
        public void Close()
        {
            if (!_running || _socketId < 0) return;

            WS_Close(_socketId, 1000, "Normal Closure");
            // OnClose will be called from JS callback
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
        /// Process dispatch queue on the calling thread.
        /// Call this from Unity's Update() for main thread event handling.
        /// </summary>
        public void Update()
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

        /// <inheritdoc />
        public void Dispose()
        {
            if (_socketId >= 0)
            {
                try { Close(); } catch { /* ignore */ }
                WebGLWsDriver.Instance.Unregister(_socketId);
                _socketId = -1;
            }
            _running = false;
        }

        // ========== Internal: Called by WebGLWsDriver ==========

        internal void HandleJsOpen()
        {
            SafeDispatch(() => OnOpen?.Invoke());
        }

        internal void HandleJsClose(ushort code, string reason)
        {
            _running = false;
            if (_socketId >= 0)
            {
                WebGLWsDriver.Instance.Unregister(_socketId);
                _socketId = -1;
            }
            SafeDispatch(() => OnClose?.Invoke(code, reason));
        }

        internal void HandleJsMessagePtr(int ptr, int len)
        {
            if (ptr == 0 || len <= 0) return;

            byte[]? rented = null;
            try
            {
                rented = ArrayPool<byte>.Shared.Rent(len);
                Marshal.Copy((IntPtr)ptr, rented, 0, len);

                // Pass to core (Span-based, no storage)
                _core.OnFrame(_sessionId, rented.AsSpan(0, len));
            }
            finally
            {
                // Free JS malloc buffer
                WS_FreeBuffer(ptr);
                if (rented != null) ArrayPool<byte>.Shared.Return(rented);
            }
        }

        internal void HandleJsError(string message)
        {
            SafeDispatch(() => OnError?.Invoke(new Exception(message)));
        }

        private void SafeDispatch(Action action)
        {
            lock (_dispatchLock)
            {
                _dispatchQueue.Enqueue(action);
            }
        }

#else
        // ========== Editor/Standalone Implementation ==========

        private ClientWebSocket? _ws;
        private Thread? _recvThread;
        private Thread? _sendThread;

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

            _running = true;

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
            EnqueueClose();
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
        /// Process dispatch queue on the calling thread.
        /// Call this from Unity's Update() for main thread event handling.
        /// </summary>
        public void Update()
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

                    if (item.Type == SendItemType.Close)
                    {
                        try
                        {
                            _ws.CloseOutputAsync(
                                WebSocketCloseStatus.NormalClosure,
                                "Normal Closure",
                                CancellationToken.None).GetAwaiter().GetResult();
                        }
                        catch (Exception ex)
                        {
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
                            cancellationToken: CancellationToken.None).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
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
                if (_ws == null) return;

                // Start with 64KB buffer, grow as needed
                _recvBuffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
                _recvLen = 0;

                while (_running && _ws.State == WebSocketState.Open)
                {
                    EnsureRecvCapacity(minFree: 8 * 1024);

                    var segment = new ArraySegment<byte>(_recvBuffer!, _recvLen, _recvBuffer!.Length - _recvLen);
                    var result = _ws.ReceiveAsync(segment, CancellationToken.None).GetAwaiter().GetResult();

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
            catch (Exception ex)
            {
                SafeDispatch(() => OnError?.Invoke(ex));
                closeCode = 1006; // Abnormal Closure
                closeReason = ex.Message;
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
