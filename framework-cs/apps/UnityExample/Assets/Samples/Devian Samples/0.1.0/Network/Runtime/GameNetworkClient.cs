#nullable enable
using System;
using Devian.Protocol.Game;

namespace Devian
{
    /// <summary>
    /// Pure C# WebSocket client for Game protocol.
    /// Implements INetTickable for unified tick management via NetTickRunner.
    /// </summary>
    public sealed class GameNetworkClient : INetTickable, IDisposable
    {
        // --- Internal components ---
        private readonly SampleGame2CStub _stub;
        private readonly Game2C.Runtime _inboundRuntime;
        private readonly NetClient _core;
        private readonly NetWsClient _transport;
        private readonly C2Game.Proxy _proxy;

        // --- Events: Transport ---
        /// <summary>Fired when connection is established.</summary>
        public event Action? OnOpen;

        /// <summary>Fired when connection is closed.</summary>
        public event Action<ushort, string>? OnClose;

        /// <summary>Fired when an error occurs.</summary>
        public event Action<Exception>? OnError;

        // --- Events: Protocol ---
        /// <summary>Fired when a Pong message is received.</summary>
        public event Action<Game2C.Pong>? OnPong;

        /// <summary>Fired when an EchoReply message is received.</summary>
        public event Action<Game2C.EchoReply>? OnEchoReply;

        // --- Properties ---
        /// <summary>Returns true if the connection is open.</summary>
        public bool IsConnected => _transport.IsOpen;

        /// <summary>
        /// Creates a new GameNetworkClient.
        /// </summary>
        public GameNetworkClient()
        {
            // 1. Create inbound stub and wire events
            _stub = new SampleGame2CStub();
            _stub.OnPongReceived += (meta, msg) => OnPong?.Invoke(msg);
            _stub.OnEchoReplyReceived += (meta, msg) => OnEchoReply?.Invoke(msg);

            // 2. Create inbound runtime
            _inboundRuntime = new Game2C.Runtime(_stub);

            // 3. Create core and transport
            _core = new NetClient(_inboundRuntime);
            _transport = new NetWsClient(_core);

            // 4. Wire transport events
            _transport.OnOpen += () => OnOpen?.Invoke();
            _transport.OnClose += (code, reason) => OnClose?.Invoke(code, reason);
            _transport.OnError += (ex) => OnError?.Invoke(ex);

            // 5. Create outbound proxy with sender adapter
            _proxy = new C2Game.Proxy(new WsSender(_transport));
        }

        // --- Public API ---

        /// <summary>
        /// Connect to the WebSocket server.
        /// </summary>
        /// <param name="url">WebSocket URL (ws:// or wss://)</param>
        public void Connect(string url)
        {
            _transport.Connect(url);
        }

        /// <summary>
        /// Close the connection gracefully.
        /// </summary>
        public void Close()
        {
            _transport.Close();
        }

        /// <summary>
        /// Send a Ping message to the server.
        /// </summary>
        /// <param name="sessionId">Session ID (default 0 for single-connection)</param>
        public void SendPing(int sessionId = 0)
        {
            var ping = new C2Game.Ping
            {
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Payload = "ping"
            };
            _proxy.SendPing(sessionId, ping);
        }

        /// <summary>
        /// Send an Echo message to the server.
        /// </summary>
        /// <param name="text">Text to echo</param>
        /// <param name="sessionId">Session ID (default 0 for single-connection)</param>
        public void SendEcho(string text, int sessionId = 0)
        {
            var echo = new C2Game.Echo
            {
                Message = text
            };
            _proxy.SendEcho(sessionId, echo);
        }

        /// <summary>
        /// Tick the transport to process pending events.
        /// Called automatically when registered with NetTickRunner.
        /// </summary>
        public void Tick()
        {
            _transport.Tick();
        }

        /// <summary>
        /// Dispose the client and release resources.
        /// </summary>
        public void Dispose()
        {
            _transport.Dispose();
        }

        // --- Sender adapter ---
        private sealed class WsSender : C2Game.ISender
        {
            private readonly NetWsClient _transport;

            public WsSender(NetWsClient transport)
            {
                _transport = transport;
            }

            public void SendTo(int sessionId, ReadOnlySpan<byte> frame)
            {
                _transport.SendFrame(frame);
            }
        }
    }
}
