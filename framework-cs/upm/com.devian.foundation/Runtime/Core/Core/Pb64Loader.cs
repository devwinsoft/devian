// SSOT: skills/devian/32-json-row-io/SKILL.md
// PB64 Loader with DVGB gzip block container support

using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Devian
{
    /// <summary>
    /// PB64 binary loader with DVGB gzip block container support.
    /// 
    /// DVGB Container format:
    /// - Magic: 4 bytes "DVGB" (ASCII)
    /// - Version: 1 byte (= 1)
    /// - BlockSize: 4 bytes little-endian (= 1048576, for reference only)
    /// - BlockCount: 4 bytes little-endian
    /// - Blocks: repeat BlockCount times
    ///   - UncompressedLen: 4 bytes little-endian
    ///   - CompressedLen: 4 bytes little-endian
    ///   - GzipBytes: CompressedLen bytes
    /// </summary>
    public static class Pb64Loader
    {
        private static readonly byte[] DVGB_MAGIC = Encoding.ASCII.GetBytes("DVGB");
        private const byte DVGB_VERSION = 1;

        /// <summary>
        /// Decode base64 and decompress if DVGB container.
        /// Returns raw pb64 binary (varint length-delimited JSON rows).
        /// </summary>
        /// <param name="base64">Base64 encoded pb64 data (may be DVGB container or raw)</param>
        /// <returns>Raw pb64 binary</returns>
        public static byte[] LoadFromBase64(string base64)
        {
            if (string.IsNullOrEmpty(base64))
                return Array.Empty<byte>();

            var bytes = Convert.FromBase64String(base64);
            return Decompress(bytes);
        }

        /// <summary>
        /// Decompress if DVGB container, otherwise return as-is.
        /// </summary>
        /// <param name="data">Raw binary (may be DVGB container or raw pb64)</param>
        /// <returns>Decompressed raw pb64 binary</returns>
        public static byte[] Decompress(byte[] data)
        {
            if (data == null || data.Length < 4)
                return data ?? Array.Empty<byte>();

            // Check for DVGB magic
            if (data[0] == DVGB_MAGIC[0] &&
                data[1] == DVGB_MAGIC[1] &&
                data[2] == DVGB_MAGIC[2] &&
                data[3] == DVGB_MAGIC[3])
            {
                return DecompressDvgbContainer(data);
            }

            // Not DVGB container, return as-is (legacy format)
            return data;
        }

        /// <summary>
        /// Decompress DVGB gzip block container.
        /// </summary>
        private static byte[] DecompressDvgbContainer(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);

            // Read header
            var magic = reader.ReadBytes(4);
            var version = reader.ReadByte();
            var blockSize = reader.ReadUInt32(); // For reference only
            var blockCount = reader.ReadUInt32();

            // Validate
            if (version != DVGB_VERSION)
                throw new InvalidDataException($"Unsupported DVGB version: {version}. Expected: {DVGB_VERSION}");

            if (blockCount == 0)
                return Array.Empty<byte>();

            // Calculate total uncompressed size
            var blockInfos = new (uint uncompressedLen, uint compressedLen)[blockCount];
            long totalUncompressed = 0;

            var headerEndPos = ms.Position;
            for (int i = 0; i < blockCount; i++)
            {
                var uncompressedLen = reader.ReadUInt32();
                var compressedLen = reader.ReadUInt32();
                blockInfos[i] = (uncompressedLen, compressedLen);
                totalUncompressed += uncompressedLen;
                ms.Position += compressedLen; // Skip data for now
            }

            // Reset to read data
            ms.Position = headerEndPos;

            // Decompress blocks
            var result = new byte[totalUncompressed];
            int resultOffset = 0;

            for (int i = 0; i < blockCount; i++)
            {
                var (uncompressedLen, compressedLen) = blockInfos[i];
                
                // Skip block header (already read)
                reader.ReadUInt32(); // uncompressedLen
                reader.ReadUInt32(); // compressedLen
                
                var gzipBytes = reader.ReadBytes((int)compressedLen);
                
                // Decompress gzip
                using var gzipStream = new GZipStream(
                    new MemoryStream(gzipBytes), 
                    CompressionMode.Decompress);
                
                int bytesRead = 0;
                while (bytesRead < uncompressedLen)
                {
                    int read = gzipStream.Read(result, resultOffset + bytesRead, (int)uncompressedLen - bytesRead);
                    if (read == 0) break;
                    bytesRead += read;
                }

                if (bytesRead != uncompressedLen)
                    throw new InvalidDataException($"Block {i}: Expected {uncompressedLen} bytes, got {bytesRead}");

                resultOffset += (int)uncompressedLen;
            }

            return result;
        }

        /// <summary>
        /// Parse raw pb64 binary (varint length-delimited JSON rows).
        /// </summary>
        /// <param name="rawBinary">Raw pb64 binary</param>
        /// <param name="parseRow">Function to parse each JSON row</param>
        public static void ParseRows(byte[] rawBinary, Action<string> parseRow)
        {
            if (rawBinary == null || rawBinary.Length == 0)
                return;

            int offset = 0;
            while (offset < rawBinary.Length)
            {
                // Read varint length
                int length = ReadVarint(rawBinary, ref offset);
                if (length <= 0 || offset + length > rawBinary.Length)
                    break;

                // Read JSON string
                var json = Encoding.UTF8.GetString(rawBinary, offset, length);
                offset += length;

                // Parse row
                parseRow(json);
            }
        }

        /// <summary>
        /// Read protobuf varint from byte array.
        /// </summary>
        private static int ReadVarint(byte[] data, ref int offset)
        {
            int result = 0;
            int shift = 0;

            while (offset < data.Length)
            {
                byte b = data[offset++];
                result |= (b & 0x7F) << shift;

                if ((b & 0x80) == 0)
                    return result;

                shift += 7;
                if (shift > 28)
                    throw new InvalidDataException("Varint too long");
            }

            throw new InvalidDataException("Unexpected end of varint");
        }
    }
}
