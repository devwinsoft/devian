namespace Devian.Core
{
    /// <summary>
    /// Envelope containing packet metadata and payload.
    /// </summary>
    public sealed class PacketEnvelope
    {
        public ushort Opcode { get; set; }
        public byte[] Payload { get; set; } = System.Array.Empty<byte>();
        public string? SessionId { get; set; }
        public object? Context { get; set; }
    }
}
