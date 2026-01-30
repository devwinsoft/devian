#nullable enable
using System;

namespace Devian
{
    /// <summary>
    /// Envelope containing packet metadata and payload for network transmission.
    /// </summary>
    public readonly struct NetPacketEnvelope
    {
        /// <summary>
        /// Message opcode (identifier).
        /// </summary>
        public int Opcode { get; }

        /// <summary>
        /// Serialized message payload.
        /// </summary>
        public byte[] Payload { get; }

        /// <summary>
        /// Optional session identifier.
        /// </summary>
        public long SessionId { get; }

        /// <summary>
        /// Optional flags for additional metadata.
        /// </summary>
        public int Flags { get; }

        /// <summary>
        /// Creates a new NetPacketEnvelope.
        /// </summary>
        public NetPacketEnvelope(int opcode, byte[] payload, long sessionId = 0, int flags = 0)
        {
            Opcode = opcode;
            Payload = payload ?? Array.Empty<byte>();
            SessionId = sessionId;
            Flags = flags;
        }
    }
}
