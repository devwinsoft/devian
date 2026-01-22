#nullable enable
using System;
using System.Buffers.Binary;

namespace Devian.Network
{
    /// <summary>
    /// Frame format V1: [opcode:int32LE][payload...]
    /// </summary>
    public static class FrameV1
    {
        /// <summary>
        /// Opcode size in bytes (int32).
        /// </summary>
        public const int OpcodeSize = 4;

        /// <summary>
        /// Try to parse a frame into opcode and payload.
        /// </summary>
        /// <param name="frame">The complete frame bytes.</param>
        /// <param name="opcode">Parsed opcode (int32 LE).</param>
        /// <param name="payload">Payload slice (zero-copy).</param>
        /// <returns>True if successfully parsed, false if frame is too short.</returns>
        public static bool TryParse(ReadOnlySpan<byte> frame, out int opcode, out ReadOnlySpan<byte> payload)
        {
            if (frame.Length < OpcodeSize)
            {
                opcode = 0;
                payload = ReadOnlySpan<byte>.Empty;
                return false;
            }

            opcode = BinaryPrimitives.ReadInt32LittleEndian(frame.Slice(0, OpcodeSize));
            payload = frame.Slice(OpcodeSize);
            return true;
        }

        /// <summary>
        /// Build a frame into the destination buffer.
        /// </summary>
        /// <param name="destination">Destination buffer (must be at least OpcodeSize + payload.Length).</param>
        /// <param name="opcode">Opcode to write.</param>
        /// <param name="payload">Payload to write.</param>
        /// <returns>Total bytes written.</returns>
        /// <exception cref="ArgumentException">Thrown if destination is too small.</exception>
        public static int Build(Span<byte> destination, int opcode, ReadOnlySpan<byte> payload)
        {
            var totalSize = OpcodeSize + payload.Length;
            if (destination.Length < totalSize)
            {
                throw new ArgumentException($"Destination buffer too small. Required: {totalSize}, Available: {destination.Length}");
            }

            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(0, OpcodeSize), opcode);
            payload.CopyTo(destination.Slice(OpcodeSize));
            return totalSize;
        }

        /// <summary>
        /// Calculate total frame size for a given payload length.
        /// </summary>
        /// <param name="payloadLength">Length of the payload.</param>
        /// <returns>Total frame size (opcode + payload).</returns>
        public static int CalculateSize(int payloadLength)
        {
            return OpcodeSize + payloadLength;
        }
    }
}
