#nullable enable
using System;
using System.Buffers;
using System.Collections.Generic;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Base class for WebSocket client MonoBehaviour.
    ///
    /// NOTE: The standard/recommended approach for network client management is:
    /// 1. Create INetTickable clients (e.g., NetWsClient) in your app code
    /// 2. Register them with a NetTickRunner for unified tick management
    /// 3. Handle protocol binding in your app layer, not in this behaviour
    ///
    /// This MonoBehaviour is provided as a convenience/optional pattern for cases where
    /// you want a self-contained WebSocket client component. It is NOT required for
    /// using the network system.
    ///
    /// Design principles:
    /// - No policy fields (url storage, connectOnStart, autoReconnect are NOT provided)
    /// - No "connected" callback (project-specific concept)
    /// - Provides minimal engine: Connect/Close/TrySend/Tick
    /// - Extension points via virtual hooks
    /// - Main-thread safety: All inbound dispatch happens on Unity main thread
    ///
    /// Subclasses implement CreateRuntime() and optionally override hooks.
    /// </summary>
    public abstract class NetWsClientBehaviourBase : MonoBehaviour
    {
        private NetClient? _core;
        private NetWsClient? _client;
        private INetRuntime? _innerRuntime;

        // Inbound queue for main-thread dispatch
        private readonly object _inboundLock = new();
        private readonly Queue<InboundItem> _inboundQueue = new();
        private readonly Queue<Action> _mainThreadActions = new();
        private bool _overflowWarned;

        /// <summary>
        /// Returns true if the WebSocket is connected.
        /// </summary>
        public bool IsConnected => _client?.IsOpen ?? false;

        // ---------- Virtual options ----------

        /// <summary>
        /// If true, inbound messages are queued and dispatched on main thread in Update().
        /// If false, inbound messages are dispatched immediately on receive thread (unsafe for Unity API).
        /// Default: true (safe).
        /// </summary>
        protected virtual bool DispatchInboundOnMainThread => true;

        /// <summary>
        /// Maximum number of inbound items in the queue.
        /// Excess items are dropped and OnInboundQueueOverflow is called once.
        /// </summary>
        protected virtual int MaxInboundQueueItems => 1024;

        /// <summary>
        /// Get sub-protocols to use for the WebSocket connection.
        /// Override to provide project-specific sub-protocols.
        /// </summary>
        protected virtual string[]? GetSubProtocols(Uri uri) => null;

        // ---------- Public API (sync, no callbacks) ----------

        /// <summary>
        /// Connect to the WebSocket server.
        /// This is the only Connect signature. No callback variant is provided.
        /// </summary>
        /// <param name="url">WebSocket URL (ws:// or wss://)</param>
        public void Connect(string url)
        {
            // 1. Dispose existing connection (immediate cleanup for reconnect)
            DisposeClient();

            // 2. Normalize URL
            var normalizedUrl = NormalizeUrl(url);

            // 3. Parse and validate URI
            Uri uri;
            try
            {
                uri = new Uri(normalizedUrl);
                ValidateUrlOrThrow(uri);
            }
            catch (Exception ex)
            {
                OnConnectFailed(url, ex);
                return;
            }

            // 4. Get sub-protocols
            var subProtocols = GetSubProtocols(uri);

            // 5. Notify hooks
            OnConnectRequested(uri);
            OnConnecting(uri, subProtocols);

            // 6. Create runtime (with optional main-thread wrapper)
            _innerRuntime = CreateRuntime();
            INetRuntime runtime = _innerRuntime;

            if (DispatchInboundOnMainThread)
            {
                runtime = new MainThreadDispatchRuntime(this);
            }

            // 7. Create core
            _core = new NetClient(runtime);

            if (DispatchInboundOnMainThread)
            {
                // Route parse errors to main thread
                _core.OnParseError = (sid, ex) => EnqueueMainThreadAction(() => OnParseError(sid, ex));
                // OnUnhandled is handled in DrainInboundQueue, so leave it null
                _core.OnUnhandled = null;
            }
            else
            {
                // Direct dispatch (unsafe for Unity API calls in handlers)
                _core.OnParseError = (sid, ex) => OnParseError(sid, ex);
                _core.OnUnhandled = (sid, op, payload) => OnUnhandledFrame(sid, op, payload);
            }

            // 8. Create client
            _client = CreateClient(uri, _core);
            HookClientEvents(_client);

            // 9. Attempt connection
            try
            {
                _client.Connect(uri.ToString(), subProtocols);
            }
            catch (Exception ex)
            {
                OnConnectFailed(url, ex);
                DisposeClient();
            }
        }

        /// <summary>
        /// Close the WebSocket connection gracefully.
        /// </summary>
        public void Close()
        {
            CloseInternal();
        }

        /// <summary>
        /// Try to send a frame. Returns false if not connected.
        /// </summary>
        /// <param name="frame">Frame bytes to send.</param>
        /// <returns>True if enqueued for sending, false if not connected.</returns>
        public bool TrySend(ReadOnlySpan<byte> frame)
        {
            if (_client == null || !_client.IsOpen)
                return false;

            try
            {
                _client.SendFrame(frame);
                return true;
            }
            catch (Exception ex)
            {
                OnClientError(ex);
                return false;
            }
        }

        /// <summary>
        /// Send a frame (byte[] convenience overload).
        /// </summary>
        public bool TrySend(byte[] frame)
        {
            if (frame == null || frame.Length == 0)
                return false;
            return TrySend(frame.AsSpan());
        }

        // ---------- Abstract: Runtime creation ----------

        /// <summary>
        /// Create the INetRuntime that handles inbound message dispatch.
        /// Called once per Connect().
        /// </summary>
        protected abstract INetRuntime CreateRuntime();

        // ---------- Virtual hooks (no policy enforcement) ----------

        /// <summary>
        /// Normalize the URL before parsing.
        /// Default: trim whitespace.
        /// </summary>
        protected virtual string NormalizeUrl(string url)
        {
            return url?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// Validate the parsed URI. Throw to reject.
        /// Default: only ws:// and wss:// schemes are allowed.
        /// Platform restrictions (e.g., WebGL) can be enforced here.
        /// </summary>
        protected virtual void ValidateUrlOrThrow(Uri uri)
        {
            if (uri.Scheme != "ws" && uri.Scheme != "wss")
            {
                throw new ArgumentException($"Invalid WebSocket scheme: {uri.Scheme}. Only ws:// and wss:// are supported.");
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL does not support ws:// (only wss:// in most browsers)
            if (uri.Scheme == "ws")
            {
                throw new PlatformNotSupportedException("WebGL requires wss:// (secure WebSocket). ws:// is not supported.");
            }
#endif
        }

        /// <summary>
        /// Called when Connect() is invoked with a valid URI.
        /// Override for logging or pre-connect setup.
        /// </summary>
        protected virtual void OnConnectRequested(Uri uri) { }

        /// <summary>
        /// Called when connection is about to be attempted (after OnConnectRequested).
        /// Useful for UI feedback showing connection in progress.
        /// </summary>
        protected virtual void OnConnecting(Uri uri, string[]? subProtocols) { }

        /// <summary>
        /// Called when connection attempt fails (URL parse error or Connect exception).
        /// </summary>
        protected virtual void OnConnectFailed(string rawUrl, Exception ex) { }

        /// <summary>
        /// Called when the WebSocket connection opens.
        /// </summary>
        protected virtual void OnOpened() { }

        /// <summary>
        /// Called when the WebSocket connection closes.
        /// </summary>
        protected virtual void OnClosed(ushort closeCode, string reason) { }

        /// <summary>
        /// Called when a transport error occurs.
        /// </summary>
        protected virtual void OnClientError(Exception ex) { }

        /// <summary>
        /// Called when a frame parse error occurs.
        /// </summary>
        protected virtual void OnParseError(int sessionId, Exception ex) { }

        /// <summary>
        /// Called when an unhandled frame (unknown opcode) is received.
        /// </summary>
        protected virtual void OnUnhandledFrame(int sessionId, int opcode, ReadOnlySpan<byte> payload) { }

        /// <summary>
        /// Called when inbound queue overflows (only once per overflow condition).
        /// Override to handle backpressure (e.g., close connection).
        /// </summary>
        protected virtual void OnInboundQueueOverflow(int queueSize, int maxSize)
        {
            Debug.LogWarning($"[NetWsClientBehaviourBase] Inbound queue overflow: {queueSize}/{maxSize} items. New messages will be dropped.");
        }

        /// <summary>
        /// Called when an exception occurs during inbound message dispatch.
        /// </summary>
        protected virtual void OnDispatchError(int sessionId, int opcode, Exception ex)
        {
            Debug.LogError($"[NetWsClientBehaviourBase] Dispatch error for opcode {opcode}: {ex}");
        }

        /// <summary>
        /// Create NetWsClient instance. Override for custom configuration.
        /// </summary>
        protected virtual NetWsClient CreateClient(Uri uri, NetClient core)
        {
            return new NetWsClient(core, sessionId: 0);
        }

        /// <summary>
        /// Hook client events. Override to add custom event handlers.
        /// </summary>
        protected virtual void HookClientEvents(NetWsClient client)
        {
            client.OnOpen += HandleOpen;
            client.OnClose += HandleClose;
            client.OnError += HandleError;
        }

        /// <summary>
        /// Unhook client events. Override if HookClientEvents was overridden.
        /// </summary>
        protected virtual void UnhookClientEvents(NetWsClient client)
        {
            client.OnOpen -= HandleOpen;
            client.OnClose -= HandleClose;
            client.OnError -= HandleError;
        }

        // ---------- Internal event handlers ----------

        private void HandleOpen()
        {
            OnOpened();
        }

        private void HandleClose(ushort code, string reason)
        {
            OnClosed(code, reason);
            DisposeClient();
        }

        private void HandleError(Exception ex)
        {
            OnClientError(ex);
        }

        // ---------- Unity lifecycle ----------

        protected virtual void Update()
        {
            // 1. Flush transport dispatch queue on main thread
            _client?.Tick();

            // 2. Process main-thread actions (parse errors, etc.)
            DrainMainThreadActions();

            // 3. Process inbound queue (if main-thread dispatch is enabled)
            if (DispatchInboundOnMainThread)
            {
                DrainInboundQueue();
            }
        }

        protected virtual void OnDestroy()
        {
            DisposeClient();
        }

        // ---------- Main-thread dispatch infrastructure ----------

        private void EnqueueMainThreadAction(Action action)
        {
            lock (_inboundLock)
            {
                _mainThreadActions.Enqueue(action);
            }
        }

        private void DrainMainThreadActions()
        {
            while (true)
            {
                Action? action;
                lock (_inboundLock)
                {
                    if (_mainThreadActions.Count == 0)
                        break;
                    action = _mainThreadActions.Dequeue();
                }

                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[NetWsClientBehaviourBase] Main thread action error: {ex}");
                }
            }
        }

        private void EnqueueInbound(int sessionId, int opcode, ReadOnlySpan<byte> payload)
        {
            var length = payload.Length;
            var buffer = ArrayPool<byte>.Shared.Rent(length);
            payload.CopyTo(buffer.AsSpan(0, length));

            lock (_inboundLock)
            {
                if (_inboundQueue.Count >= MaxInboundQueueItems)
                {
                    // Drop the message and return buffer
                    ArrayPool<byte>.Shared.Return(buffer);

                    if (!_overflowWarned)
                    {
                        _overflowWarned = true;
                        var size = _inboundQueue.Count;
                        var max = MaxInboundQueueItems;
                        // Schedule overflow notification on main thread
                        _mainThreadActions.Enqueue(() => OnInboundQueueOverflow(size, max));
                    }
                    return;
                }

                _inboundQueue.Enqueue(new InboundItem
                {
                    SessionId = sessionId,
                    Opcode = opcode,
                    Buffer = buffer,
                    Length = length
                });
            }
        }

        private void DrainInboundQueue()
        {
            if (_innerRuntime == null)
                return;

            while (true)
            {
                InboundItem item;
                lock (_inboundLock)
                {
                    if (_inboundQueue.Count == 0)
                    {
                        // Reset overflow flag when queue is empty
                        _overflowWarned = false;
                        break;
                    }
                    item = _inboundQueue.Dequeue();
                }

                try
                {
                    var payload = item.Buffer.AsSpan(0, item.Length);
                    var handled = _innerRuntime.TryDispatchInbound(item.SessionId, item.Opcode, payload);

                    if (!handled)
                    {
                        OnUnhandledFrame(item.SessionId, item.Opcode, payload);
                    }
                }
                catch (Exception ex)
                {
                    OnDispatchError(item.SessionId, item.Opcode, ex);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(item.Buffer);
                }
            }
        }

        private void ClearInboundQueue()
        {
            lock (_inboundLock)
            {
                while (_inboundQueue.Count > 0)
                {
                    var item = _inboundQueue.Dequeue();
                    ArrayPool<byte>.Shared.Return(item.Buffer);
                }
                _mainThreadActions.Clear();
                _overflowWarned = false;
            }
        }

        // ---------- Internal helpers ----------

        private void CloseInternal()
        {
            if (_client == null) return;

            // IMPORTANT: Do NOT unhook events here.
            // OnClose event must be received to update state properly.
            // DisposeClient() will be called in HandleClose() after OnClosed hook.
            try
            {
                _client.Close();
            }
            catch
            {
                // If Close fails, force cleanup
                DisposeClient();
            }
        }

        private void DisposeClient()
        {
            // Clear inbound queue first (return ArrayPool buffers)
            ClearInboundQueue();

            if (_client != null)
            {
                try
                {
                    UnhookClientEvents(_client);
                    _client.Dispose();
                }
                catch
                {
                    // ignore
                }
                finally
                {
                    _client = null;
                }
            }

            _core = null;
            _innerRuntime = null;
        }

        // ---------- Nested types ----------

        private struct InboundItem
        {
            public int SessionId;
            public int Opcode;
            public byte[] Buffer;
            public int Length;
        }

        /// <summary>
        /// INetRuntime wrapper that queues inbound messages for main-thread dispatch.
        /// </summary>
        private sealed class MainThreadDispatchRuntime : INetRuntime
        {
            private readonly NetWsClientBehaviourBase _behaviour;

            public MainThreadDispatchRuntime(NetWsClientBehaviourBase behaviour)
            {
                _behaviour = behaviour;
            }

            public bool TryDispatchInbound(int sessionId, int opcode, ReadOnlySpan<byte> payload)
            {
                // Queue for main-thread dispatch
                _behaviour.EnqueueInbound(sessionId, opcode, payload);

                // Always return true to prevent NetClient.OnUnhandled from being called on receive thread
                // Actual unhandled detection happens in DrainInboundQueue on main thread
                return true;
            }
        }
    }
}
