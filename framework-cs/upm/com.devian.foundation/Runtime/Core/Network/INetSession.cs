#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Devian
{
    /// <summary>
    /// Network session interface for protocol-agnostic session management.
    /// Generated Proxy depends only on this interface, not concrete implementations.
    /// </summary>
    public interface INetSession
    {
        /// <summary>
        /// Current connection state.
        /// </summary>
        NetClientState State { get; }

        /// <summary>
        /// Fired when the connection is opened.
        /// </summary>
        event Action? OnOpen;

        /// <summary>
        /// Fired when the connection is closed.
        /// </summary>
        event Action<ushort, string>? OnClose;

        /// <summary>
        /// Fired when an error occurs.
        /// </summary>
        event Action<Exception>? OnError;

        /// <summary>
        /// Process network events. Call from Update() loop.
        /// </summary>
        void Tick();

        /// <summary>
        /// Connect to server asynchronously.
        /// </summary>
        Task ConnectAsync(CancellationToken ct = default);

        /// <summary>
        /// Close connection asynchronously.
        /// </summary>
        Task CloseAsync(CancellationToken ct = default);

        /// <summary>
        /// Send a frame to the server.
        /// </summary>
        /// <param name="frame">Frame data to send.</param>
        void SendTo(ReadOnlySpan<byte> frame);
    }
}
