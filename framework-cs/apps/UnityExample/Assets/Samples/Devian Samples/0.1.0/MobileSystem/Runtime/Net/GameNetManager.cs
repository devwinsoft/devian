#nullable enable
using System;
using UnityEngine;
using Devian.Protocol.Game;

namespace Devian
{
    /// <summary>
    /// Unity MonoBehaviour manager for Game protocol network client.
    /// Singleton: CompoSingleton guarantees single instance per scene.
    /// Owns Stub (Game2C for inbound), Proxy (C2Game for outbound), and a session host.
    /// </summary>
    public sealed partial class GameNetManager : CompoSingleton<GameNetManager>
    {
        private readonly Game2CStub _stub = new();
        private readonly C2Game.Proxy _proxy = new();
        private ClientSessionHost? _sessionHost;

        /// <summary>
        /// Static access to the outbound proxy. Use GameNetManager.Proxy to send messages.
        /// </summary>
        public static C2Game.Proxy Proxy => Instance._proxy;

        public bool IsConnected => _sessionHost?.IsConnected ?? false;
        public string Url => _sessionHost?.Url ?? string.Empty;
        public string LastError => _sessionHost?.LastError ?? string.Empty;

        public event Action? OnOpen;
        public event Action<ushort, string>? OnClose;
        public event Action<Exception>? OnError;

        protected override void Awake()
        {
            base.Awake();
            _sessionHost = new ClientSessionHost(_stub, _proxy);
            _sessionHost.OnOpen += HandleOpen;
            _sessionHost.OnClose += HandleClose;
            _sessionHost.OnError += HandleError;
        }

        private void Update()
        {
            _sessionHost?.Tick();
        }

        protected override void OnDestroy()
        {
            if (_sessionHost != null)
            {
                _sessionHost.OnOpen -= HandleOpen;
                _sessionHost.OnClose -= HandleClose;
                _sessionHost.OnError -= HandleError;
                _sessionHost.Dispose();
                _sessionHost = null;
            }

            base.OnDestroy();
        }

        public void Connect(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                Debug.LogError("[GameNetManager] URL cannot be empty");
                return;
            }

            SessionHost.Connect(url);
        }

        public void Disconnect()
        {
            _sessionHost?.Disconnect();
        }

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

        private ClientSessionHost SessionHost =>
            _sessionHost ?? throw new InvalidOperationException("ClientSessionHost is not initialized.");
    }
}
