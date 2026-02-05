#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Devian
{
    /// <summary>
    /// Interface for network clients with state management.
    /// Extends INetTickable for unified tick-based processing.
    /// </summary>
    public interface INetClient : INetTickable, IDisposable
    {
        /// <summary>
        /// Current connection state.
        /// </summary>
        NetClientState State { get; }

        /// <summary>
        /// Connect to the server asynchronously.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        Task ConnectAsync(CancellationToken ct = default);

        /// <summary>
        /// Close the connection gracefully.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        Task CloseAsync(CancellationToken ct = default);

        /// <summary>
        /// Fired when connection state changes.
        /// </summary>
        event Action<NetClientState>? OnStateChanged;

        /// <summary>
        /// Fired when an error occurs.
        /// </summary>
        event Action<Exception>? OnError;
    }
}
