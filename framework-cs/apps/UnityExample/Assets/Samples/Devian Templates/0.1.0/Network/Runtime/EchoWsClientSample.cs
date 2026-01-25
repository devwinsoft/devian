#nullable enable
using System;
using UnityEngine;
using Devian;
using Devian.Protocol.Game;

namespace Devian
{
    /// <summary>
    /// Online-only WebSocket client sample for TS GameServer.
    /// 
    /// Protocol direction:
    /// - Outbound (send): C2Game.Proxy (Ping, Echo)
    /// - Inbound (receive): Game2C.Runtime + Game2C.Stub (Pong, EchoReply)
    /// 
    /// Usage:
    /// 1. Start TS GameServer: cd framework-ts/apps/GameServer && npm start
    /// 2. Attach to a GameObject
    /// 3. In Play mode, use Inspector buttons: Connect / Disconnect / Send Ping / Send Echo
    /// 
    /// Note: No offline mode. No auto-send on connect.
    /// </summary>
    public class EchoWsClientSample : NetWsClientBehaviourBase
    {
        [Header("Connection Settings")]
        [SerializeField] private string url = "ws://localhost:8080";

        [Header("Message Settings")]
        [SerializeField] private string pingPayload = "hello";
        [SerializeField] private string echoMessage = "echo test";

        private C2Game.Proxy? _proxy;

        /// <summary>
        /// Connection state for Editor UI.
        /// </summary>
        public bool IsConnected { get; private set; }

        // ========================================
        // Public API (called by CustomEditor Inspector buttons)
        // ========================================

        /// <summary>
        /// Connect using the inspector URL.
        /// Called by CustomEditor Inspector button.
        /// </summary>
        public void ConnectWithInspectorUrl()
        {
            Connect(url);
        }

        /// <summary>
        /// Close the connection.
        /// Called by CustomEditor Inspector button.
        /// </summary>
        public void Disconnect()
        {
            Close();
        }

        /// <summary>
        /// Send Ping message to server.
        /// Called by CustomEditor Inspector button.
        /// </summary>
        public void SendPing()
        {
            if (!IsConnected || _proxy == null)
            {
                Debug.LogWarning("[EchoWsClientSample] Cannot send Ping: not connected");
                return;
            }

            var ping = new C2Game.Ping
            {
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Payload = pingPayload
            };
            _proxy.SendPing(0, ping);
            Debug.Log($"[EchoWsClientSample] Sent Ping: payload={pingPayload}");
        }

        /// <summary>
        /// Send Echo message to server.
        /// Called by CustomEditor Inspector button.
        /// </summary>
        public void SendEcho()
        {
            if (!IsConnected || _proxy == null)
            {
                Debug.LogWarning("[EchoWsClientSample] Cannot send Echo: not connected");
                return;
            }

            var echo = new C2Game.Echo { Message = echoMessage };
            _proxy.SendEcho(0, echo);
            Debug.Log($"[EchoWsClientSample] Sent Echo: message={echoMessage}");
        }

        // ========================================
        // Required override
        // ========================================

        protected override INetRuntime CreateRuntime()
        {
            Debug.Log("[EchoWsClientSample] Creating Game2C.Runtime for inbound dispatch");
            return new Game2C.Runtime(new GameS2CStub());
        }

        // ========================================
        // Optional hook overrides (logging)
        // ========================================

        protected override void OnConnectRequested(Uri uri)
        {
            Debug.Log($"[EchoWsClientSample] Connecting to: {uri}");
        }

        protected override void OnConnectFailed(string rawUrl, Exception ex)
        {
            Debug.LogError($"[EchoWsClientSample] Connect failed: url={rawUrl}\n{ex}");
        }

        protected override void OnOpened()
        {
            Debug.Log("[EchoWsClientSample] Connection opened");
            IsConnected = true;

            // Create proxy for outbound messages (C2Game)
            // Always create new proxy on connect (old one is cleared in OnClosed)
            _proxy = new C2Game.Proxy(new GameSender(this));

            // NO auto-send on connect - use Inspector buttons only
        }

        protected override void OnClosed(ushort closeCode, string reason)
        {
            Debug.Log($"[EchoWsClientSample] Connection closed: code={closeCode} reason={reason}");
            IsConnected = false;
            _proxy = null; // Clear proxy on disconnect (will be recreated on next connect)
        }

        protected override void OnClientError(Exception ex)
        {
            Debug.LogException(ex);
        }

        protected override void OnParseError(int sessionId, Exception ex)
        {
            Debug.LogError($"[EchoWsClientSample] Parse error (session={sessionId}): {ex.Message}");
        }

        protected override void OnUnhandledFrame(int sessionId, int opcode, ReadOnlySpan<byte> payload)
        {
            Debug.LogWarning($"[EchoWsClientSample] Unhandled frame: opcode={opcode} len={payload.Length}");
        }

        // ========================================
        // Internal types
        // ========================================

        private sealed class GameSender : C2Game.ISender
        {
            private readonly EchoWsClientSample _owner;
            public GameSender(EchoWsClientSample owner) => _owner = owner;
            public void SendTo(int sessionId, ReadOnlySpan<byte> frame) => _owner.TrySend(frame);
        }

        private sealed class GameS2CStub : Game2C.Stub
        {
            protected override void OnPong(Game2C.EnvelopeMeta meta, Game2C.Pong message)
            {
                Debug.Log($"[Game2C] OnPong sid={meta.SessionId} ts={message.Timestamp} serverTime={message.ServerTime}");
            }

            protected override void OnEchoReply(Game2C.EnvelopeMeta meta, Game2C.EchoReply message)
            {
                Debug.Log($"[Game2C] OnEchoReply sid={meta.SessionId} msg={message.Message} echoedAt={message.EchoedAt}");
            }
        }
    }
}
