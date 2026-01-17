#nullable enable
using System;
using UnityEngine;
using Devian.Network;
using Devian.Unity.Network;
using Devian.Network.Sample;

namespace Devian.Sample
{
    /// <summary>
    /// Sample WebSocket client using generated C2Sample.Runtime.
    /// Demonstrates how to extend WebSocketClientBehaviourBase with codegen Runtime.
    /// 
    /// Usage:
    /// 1. Attach to a GameObject
    /// 2. Set the URL in the inspector
    /// 3. Use ContextMenu or call Connect() from code
    /// </summary>
    public class EchoWsClientSample : WebSocketClientBehaviourBase
    {
        [Header("Sample Settings")]
        [SerializeField] private string url = "wss://localhost/ws";

        private C2Sample.Proxy? _proxy;

        // ---------- Public API ----------

        /// <summary>
        /// Connect using the inspector URL.
        /// </summary>
        [ContextMenu("Connect")]
        public void ConnectWithInspectorUrl()
        {
            Connect(url);
        }

        /// <summary>
        /// Close the connection.
        /// </summary>
        [ContextMenu("Disconnect")]
        public void Disconnect()
        {
            Close();
        }

        // ---------- Required override ----------

        protected override INetRuntime CreateRuntime()
        {
            Debug.Log("[EchoWsClientSample] Creating generated C2Sample.Runtime");

            var stub = new SampleC2Stub();
            return new C2Sample.Runtime(stub);
        }

        // ---------- Optional hook overrides (logging) ----------

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

            // Create proxy for sending messages
            _proxy ??= new C2Sample.Proxy(new SampleSender(this));

            // Send sample messages
            var ping = new C2Sample.Ping
            {
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Payload = "hello"
            };
            _proxy.SendPing(0, ping);

            var echo = new C2Sample.Echo { Message = "echo test" };
            _proxy.SendEcho(0, echo);

            Debug.Log("[EchoWsClientSample] Sent Ping and Echo messages");
        }

        protected override void OnClosed(ushort closeCode, string reason)
        {
            Debug.Log($"[EchoWsClientSample] Connection closed: code={closeCode} reason={reason}");
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

        // ---------- Internal types ----------

        /// <summary>
        /// Sender adapter for C2Sample.Proxy.
        /// Bridges Proxy to WebSocketClientBehaviourBase.TrySend().
        /// </summary>
        private sealed class SampleSender : C2Sample.ISender
        {
            private readonly EchoWsClientSample _owner;

            public SampleSender(EchoWsClientSample owner) => _owner = owner;

            public void SendTo(int sessionId, ReadOnlySpan<byte> frame)
            {
                _owner.TrySend(frame);
            }
        }

        /// <summary>
        /// Stub handler for C2Sample messages.
        /// Inbound dispatch is handled by C2Sample.Runtime - this only contains content logic.
        /// </summary>
        private sealed class SampleC2Stub : C2Sample.Stub
        {
            protected override void OnPing(C2Sample.EnvelopeMeta meta, C2Sample.Ping message)
            {
                Debug.Log($"[C2Sample] OnPing sid={meta.SessionId} ts={message.Timestamp} payload={message.Payload}");
            }

            protected override void OnEcho(C2Sample.EnvelopeMeta meta, C2Sample.Echo message)
            {
                Debug.Log($"[C2Sample] OnEcho sid={meta.SessionId} msg={message.Message}");
            }
        }
    }
}
