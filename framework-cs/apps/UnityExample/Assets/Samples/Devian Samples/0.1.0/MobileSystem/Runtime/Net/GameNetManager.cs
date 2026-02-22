#nullable enable
using System;
using UnityEngine;
using Devian.Protocol.Game;

namespace Devian
{
    /// <summary>
    /// Unity MonoBehaviour manager for Game protocol network client.
    /// Singleton: CompoSingleton guarantees single instance per scene.
    /// Owns Stub (Game2C for inbound), Proxy (C2Game for outbound), and Connector.
    /// Fields are initialized at declaration (non-nullable).
    /// </summary>
    public sealed partial class GameNetManager : CompoSingleton<GameNetManager>
    {
        // --- Internal state (initialized at declaration, non-nullable) ---
        private readonly Game2CStub _stub = new();
        private readonly C2Game.Proxy _proxy = new();
        private readonly INetConnector _connector = new NetWsConnector();

        // --- Static access ---

        /// <summary>
        /// Static access to the outbound proxy. Use GameNetManager.Proxy to send messages.
        /// </summary>
        public static C2Game.Proxy Proxy => Instance._proxy;

        // --- Public properties (delegated to _proxy) ---

        public bool IsConnected => _proxy.IsConnected;

        public string Url => _proxy.Url;

        public string LastError => _proxy.LastError;

        // --- Events ---
        public event Action? OnOpen;
        public event Action<ushort, string>? OnClose;
        public event Action<Exception>? OnError;

        // --- Unity lifecycle ---

        protected override void Awake()
        {
            base.Awake();

            // Subscribe to proxy events (fields already initialized at declaration)
            _proxy.OnOpen += HandleOpen;
            _proxy.OnClose += HandleClose;
            _proxy.OnError += HandleError;
        }

        private void Update()
        {
            _proxy.Tick();
        }

        protected override void OnDestroy()
        {
            _proxy.OnOpen -= HandleOpen;
            _proxy.OnClose -= HandleClose;
            _proxy.OnError -= HandleError;

            _proxy.Dispose();
            base.OnDestroy();
        }

        // --- Public API ---

        public void Connect(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                Debug.LogError("[GameNetManager] URL cannot be empty");
                return;
            }

            _proxy.Connect(_stub, url, _connector);
        }

        public void Disconnect()
        {
            _proxy.Disconnect();
        }

        // --- Event handlers ---

        private void HandleOpen()
        {
            Debug.Log("[GameNetManager] Connected!");
            OnOpen?.Invoke();
        }

        private void HandleClose(ushort code, string reason)
        {
            Debug.Log($"[GameNetManager] Closed: code={code}, reason={reason}");
            OnClose?.Invoke(code, reason);
        }

        private void HandleError(Exception ex)
        {
            Debug.LogError($"[GameNetManager] Error: {ex.Message}");
            OnError?.Invoke(ex);
        }
    }
}
