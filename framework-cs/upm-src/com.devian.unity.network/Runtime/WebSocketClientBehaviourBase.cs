#nullable enable
using System;
using UnityEngine;
using Devian.Network;
using Devian.Network.Transports;

namespace Devian.Unity.Network
{
    /// <summary>
    /// Base class for WebSocket client MonoBehaviour.
    /// 
    /// Design principles:
    /// - No policy fields (url storage, connectOnStart, autoReconnect are NOT provided)
    /// - No "connected" callback (project-specific concept)
    /// - Provides minimal engine: Connect/Close/TrySend/Update
    /// - Extension points via virtual hooks
    /// 
    /// Subclasses implement CreateRuntime() and optionally override hooks.
    /// </summary>
    public abstract class WebSocketClientBehaviourBase : MonoBehaviour
    {
        private NetworkClient? _core;
        private WebSocketClient? _client;

        /// <summary>
        /// Returns true if the WebSocket is connected.
        /// </summary>
        public bool IsConnected => _client?.IsOpen ?? false;

        // ---------- Public API (sync, no callbacks) ----------

        /// <summary>
        /// Connect to the WebSocket server.
        /// This is the only Connect signature. No callback variant is provided.
        /// </summary>
        /// <param name="url">WebSocket URL (ws:// or wss://)</param>
        public void Connect(string url)
        {
            // 1. Close existing connection
            CloseInternal();

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

            // 4. Notify hook
            OnConnectRequested(uri);

            // 5. Create runtime and core
            var runtime = CreateRuntime();
            _core = new NetworkClient(runtime);
            _core.OnParseError = (sid, ex) => OnParseError(sid, ex);
            _core.OnUnhandled = (sid, op, payload) => OnUnhandledFrame(sid, op, payload);

            // 6. Create client
            _client = CreateClient(uri, _core);
            HookClientEvents(_client);

            // 7. Attempt connection
            try
            {
                _client.Connect(uri.ToString());
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
        /// Create WebSocketClient instance. Override for custom configuration.
        /// </summary>
        protected virtual WebSocketClient CreateClient(Uri uri, NetworkClient core)
        {
            return new WebSocketClient(core, sessionId: 0);
        }

        /// <summary>
        /// Hook client events. Override to add custom event handlers.
        /// </summary>
        protected virtual void HookClientEvents(WebSocketClient client)
        {
            client.OnOpen += HandleOpen;
            client.OnClose += HandleClose;
            client.OnError += HandleError;
        }

        /// <summary>
        /// Unhook client events. Override if HookClientEvents was overridden.
        /// </summary>
        protected virtual void UnhookClientEvents(WebSocketClient client)
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
        }

        private void HandleError(Exception ex)
        {
            OnClientError(ex);
        }

        // ---------- Unity lifecycle ----------

        protected virtual void Update()
        {
            // Flush dispatch queue on main thread
            _client?.Update();
        }

        protected virtual void OnDestroy()
        {
            DisposeClient();
        }

        // ---------- Internal helpers ----------

        private void CloseInternal()
        {
            if (_client == null) return;

            try
            {
                UnhookClientEvents(_client);
                _client.Close();
            }
            catch
            {
                // ignore
            }
        }

        private void DisposeClient()
        {
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
        }
    }
}
