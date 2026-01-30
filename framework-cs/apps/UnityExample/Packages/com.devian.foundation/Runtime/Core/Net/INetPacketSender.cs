#nullable enable
using System.Threading;
using System.Threading.Tasks;

namespace Devian
{
    /// <summary>
    /// Interface for sending packets over a network transport.
    /// </summary>
    public interface INetPacketSender
    {
        /// <summary>
        /// Sends a packet envelope asynchronously.
        /// </summary>
        /// <param name="envelope">The packet envelope to send.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A task representing the send operation.</returns>
        ValueTask SendAsync(NetPacketEnvelope envelope, CancellationToken ct = default);
    }
}
