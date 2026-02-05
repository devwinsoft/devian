#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Devian
{
    /// <summary>
    /// Interface for network transport implementations (WebSocket, TCP, etc.).
    /// Transport handles the low-level connection and data transfer.
    /// </summary>
    public interface INetTransport : IDisposable
    {
        /// <summary>
        /// Returns true if the transport is connected.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Connect to the server asynchronously.
        /// </summary>
        /// <param name="url">Server URL.</param>
        /// <param name="ct">Cancellation token.</param>
        Task ConnectAsync(string url, CancellationToken ct = default);

        /// <summary>
        /// Close the connection gracefully.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        Task CloseAsync(CancellationToken ct = default);

        /// <summary>
        /// Process pending events. Call once per frame from main thread.
        /// </summary>
        void Tick();

        /// <summary>
        /// Send a frame to the server.
        /// </summary>
        /// <param name="frame">Frame bytes to send.</param>
        void SendFrame(ReadOnlySpan<byte> frame);

        /// <summary>
        /// Fired when the connection is established.
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
    }
}
