#nullable enable
using System;
using Devian.Protocol.Game;

namespace Devian
{
    /// <summary>
    /// Sample implementation of Game2C.Stub for handling Pong and EchoReply messages.
    /// Exposes events that GameNetworkClient can subscribe to.
    /// </summary>
    internal sealed class SampleGame2CStub : Game2C.Stub
    {
        /// <summary>
        /// Fired when a Pong message is received.
        /// </summary>
        public event Action<Game2C.EnvelopeMeta, Game2C.Pong>? OnPongReceived;

        /// <summary>
        /// Fired when an EchoReply message is received.
        /// </summary>
        public event Action<Game2C.EnvelopeMeta, Game2C.EchoReply>? OnEchoReplyReceived;

        public SampleGame2CStub(Game2C.ICodec? codec = null) : base(codec)
        {
        }

        protected override void OnPong(Game2C.EnvelopeMeta meta, Game2C.Pong message)
        {
            OnPongReceived?.Invoke(meta, message);
        }

        protected override void OnEchoReply(Game2C.EnvelopeMeta meta, Game2C.EchoReply message)
        {
            OnEchoReplyReceived?.Invoke(meta, message);
        }
    }
}
