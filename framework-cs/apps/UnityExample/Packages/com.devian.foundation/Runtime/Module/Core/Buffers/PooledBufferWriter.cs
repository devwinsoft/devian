#nullable enable
using System;
using System.Buffers;

namespace Devian
{
    /// <summary>
    /// IBufferWriter implementation backed by ArrayPool.
    /// Zero GC allocation for protocol encoding.
    /// </summary>
    public sealed class PooledBufferWriter : IBufferWriter<byte>, IDisposable
    {
        private byte[] _buffer;
        private int _written;
        private bool _disposed;

        /// <summary>
        /// Creates a new PooledBufferWriter with the specified initial capacity.
        /// </summary>
        /// <param name="initialCapacity">Initial buffer size hint (will be rounded up by ArrayPool).</param>
        public PooledBufferWriter(int initialCapacity = 256)
        {
            _buffer = ArrayPool<byte>.Shared.Rent(Math.Max(initialCapacity, 256));
            _written = 0;
            _disposed = false;
        }

        /// <summary>
        /// Number of bytes written to the buffer.
        /// </summary>
        public int WrittenCount => _written;

        /// <summary>
        /// Span of written bytes.
        /// </summary>
        public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _written);

        /// <summary>
        /// Memory of written bytes.
        /// </summary>
        public ReadOnlyMemory<byte> WrittenMemory => _buffer.AsMemory(0, _written);

        /// <inheritdoc />
        public void Advance(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (_written + count > _buffer.Length)
                throw new InvalidOperationException("Cannot advance past the end of the buffer.");

            _written += count;
        }

        /// <inheritdoc />
        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return _buffer.AsMemory(_written);
        }

        /// <inheritdoc />
        public Span<byte> GetSpan(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return _buffer.AsSpan(_written);
        }

        /// <summary>
        /// Resets the writer for reuse without returning the buffer to the pool.
        /// </summary>
        public void Reset()
        {
            _written = 0;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_buffer != null)
            {
                ArrayPool<byte>.Shared.Return(_buffer);
                _buffer = null!;
            }
            _written = 0;
        }

        private void EnsureCapacity(int sizeHint)
        {
            if (sizeHint <= 0) sizeHint = 1;

            var available = _buffer.Length - _written;
            if (available >= sizeHint) return;

            // Need to grow: rent larger buffer and copy
            var newSize = _buffer.Length;
            var required = _written + sizeHint;
            while (newSize < required)
                newSize *= 2;

            var newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
            _buffer.AsSpan(0, _written).CopyTo(newBuffer.AsSpan(0, _written));

            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = newBuffer;
        }
    }
}
