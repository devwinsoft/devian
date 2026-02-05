#nullable enable
using UnityEngine;
using Devian.Protocol.Game;

namespace Devian
{
    /// <summary>
    /// Concrete stub for Game2C inbound messages.
    /// Extend via partial class to implement handlers.
    /// No external registration - all handling is internal.
    /// </summary>
    public partial class Game2CStub : Game2C.Stub
    {
        public Game2CStub(Game2C.ICodec? codec = null) : base(codec) { }

        protected override void OnPong(Game2C.EnvelopeMeta meta, Game2C.Pong message)
        {
            Debug.Log($"[Game2CStub] OnPong received: timestamp={message.Timestamp}");
            OnPongImpl(meta, message);
        }

        protected override void OnEchoReply(Game2C.EnvelopeMeta meta, Game2C.EchoReply message)
        {
            Debug.Log($"[Game2CStub] OnEchoReply received: message={message.Message}");
            OnEchoReplyImpl(meta, message);
        }

        // --- Partial hooks for extension ---
        partial void OnPongImpl(Game2C.EnvelopeMeta meta, Game2C.Pong message);
        partial void OnEchoReplyImpl(Game2C.EnvelopeMeta meta, Game2C.EchoReply message);
    }
}
