#nullable enable
using System;
using UnityEngine;
using Devian.Network;
using Devian.Unity.Network;
using Devian.Network.Sample;

namespace Devian.Sample
{
    /// <summary>
    /// Online-only WebSocket client sample for TS SampleServer.
    /// 
    /// Protocol direction:
    /// - Outbound (send): C2Sample.Proxy (Ping, Echo)
    /// - Inbound (receive): Sample2C.Runtime + Sample2C.Stub (Pong, EchoReply)
    /// 
    /// Usage:
    /// 1. Start TS SampleServer: cd framework-ts/apps/SampleServer && npm start
    /// 2. Attach to a GameObject
    /// 3. In Play mode, use Inspector buttons: Connect / Disconnect / Send Ping / Send Echo
    /// 
    /// Note: No offline mode. No auto-send on connect.
    /// </summary>
    public class EchoWsClientSample : WebSocketClientBehaviourBase
    {
        [Header("Connection Settings")]
        [SerializeField] private string url = "ws://localhost:8080";

        [Header("Message Settings")]
        [SerializeField] private string pingPayload = "hello";
        [SerializeField] private string echoMessage = "echo test";

        private C2Sample.Proxy? _proxy;

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
        public new void Disconnect()
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

            var ping = new C2Sample.Ping
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

            var echo = new C2Sample.Echo { Message = echoMessage };
            _proxy.SendEcho(0, echo);
            Debug.Log($"[EchoWsClientSample] Sent Echo: message={echoMessage}");
        }

        // ========================================
        // Required override
        // ========================================

        protected override INetRuntime CreateRuntime()
        {
            Debug.Log("[EchoWsClientSample] Creating Sample2C.Runtime for inbound dispatch");
            return new Sample2C.Runtime(new SampleS2CStub());
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

            // Create proxy for outbound messages (C2Sample)
            _proxy ??= new C2Sample.Proxy(new SampleSender(this));

            // NO auto-send on connect - use Inspector buttons only
        }

        protected override void OnClosed(ushort closeCode, string reason)
        {
            Debug.Log($"[EchoWsClientSample] Connection closed: code={closeCode} reason={reason}");
            IsConnected = false;
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

        private sealed class SampleSender : C2Sample.ISender
        {
            private readonly EchoWsClientSample _owner;
            public SampleSender(EchoWsClientSample owner) => _owner = owner;
            public void SendTo(int sessionId, ReadOnlySpan<byte> frame) => _owner.TrySend(frame);
        }

        private sealed class SampleS2CStub : Sample2C.Stub
        {
            protected override void OnPong(Sample2C.EnvelopeMeta meta, Sample2C.Pong message)
            {
                Debug.Log($"[Sample2C] OnPong sid={meta.SessionId} ts={message.Timestamp} serverTime={message.ServerTime}");
            }

            protected override void OnEchoReply(Sample2C.EnvelopeMeta meta, Sample2C.EchoReply message)
            {
                Debug.Log($"[Sample2C] OnEchoReply sid={meta.SessionId} msg={message.Message} echoedAt={message.EchoedAt}");
            }
        }
    }
}
