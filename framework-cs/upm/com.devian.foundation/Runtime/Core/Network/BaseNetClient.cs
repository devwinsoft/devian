#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Devian
{
    /// <summary>
    /// Base class for network clients with common state management logic.
    /// Protocol-specific message handling is NOT in this class.
    /// Implements INetSession for protocol-agnostic session management.
    /// </summary>
    public class BaseNetClient : INetClient, INetSession
    {
        private readonly INetTransport _transport;
        private readonly string _url;
        private readonly object _stateLock = new();
        private NetClientState _state = NetClientState.Disconnected;
        private bool _disposed;

        /// <inheritdoc />
        public NetClientState State
        {
            get { lock (_stateLock) return _state; }
            private set
            {
                NetClientState old;
                lock (_stateLock)
                {
                    if (_state == value) return;
                    old = _state;
                    _state = value;
                }
                OnStateChanged?.Invoke(value);
            }
        }

        /// <inheritdoc />
        public event Action<NetClientState>? OnStateChanged;

        /// <inheritdoc />
        public event Action<Exception>? OnError;

        /// <summary>
        /// Fired when the connection is opened.
        /// </summary>
        public event Action? OnOpen;

        /// <summary>
        /// Fired when the connection is closed.
        /// </summary>
        public event Action<ushort, string>? OnClose;

        /// <summary>
        /// Creates a new BaseNetClient with the specified transport and URL.
        /// </summary>
        /// <param name="transport">Transport implementation.</param>
        /// <param name="url">Server URL to connect to.</param>
        public BaseNetClient(INetTransport transport, string url)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _url = url ?? throw new ArgumentNullException(nameof(url));

            // Wire transport events
            _transport.OnOpen += HandleTransportOpen;
            _transport.OnClose += HandleTransportClose;
            _transport.OnError += HandleTransportError;
        }

        /// <inheritdoc />
        public virtual async Task ConnectAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();

            lock (_stateLock)
            {
                if (_state == NetClientState.Connected || _state == NetClientState.Connecting)
                    return; // Already connected or connecting

                if (_state == NetClientState.Closing)
                    throw new InvalidOperationException("Cannot connect while closing.");
            }

            State = NetClientState.Connecting;

            try
            {
                await _transport.ConnectAsync(_url, ct).ConfigureAwait(false);
                // State will be set to Connected via OnOpen event
            }
            catch (Exception ex)
            {
                // Only transition to Faulted if we are still Connecting.
                // If OnClose already moved us to Disconnected, do not overwrite.
                if (State == NetClientState.Connecting)
                    State = NetClientState.Faulted;
                OnError?.Invoke(ex);
                throw;
            }
        }

        /// <inheritdoc />
        public virtual async Task CloseAsync(CancellationToken ct = default)
        {
            lock (_stateLock)
            {
                if (_state == NetClientState.Disconnected ||
                    _state == NetClientState.Closing ||
                    _state == NetClientState.Faulted)
                    return; // Already closed/closing
            }

            State = NetClientState.Closing;

            try
            {
                await _transport.CloseAsync(ct).ConfigureAwait(false);
                // State will be set to Disconnected via OnClose event
            }
            catch (Exception ex)
            {
                State = NetClientState.Faulted;
                OnError?.Invoke(ex);
            }
        }

        /// <inheritdoc />
        public virtual void Tick()
        {
            if (_disposed) return;
            _transport.Tick();
        }

        /// <summary>
        /// Send a frame through the transport.
        /// </summary>
        /// <param name="frame">Frame data to send.</param>
        public void SendTo(ReadOnlySpan<byte> frame)
        {
            ThrowIfDisposed();
            _transport.SendFrame(frame);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose resources.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;

            if (disposing)
            {
                _transport.OnOpen -= HandleTransportOpen;
                _transport.OnClose -= HandleTransportClose;
                _transport.OnError -= HandleTransportError;
                _transport.Dispose();
            }

            State = NetClientState.Disconnected;
        }

        /// <summary>
        /// Get the underlying transport for protocol-specific operations.
        /// </summary>
        protected INetTransport Transport => _transport;

        private void HandleTransportOpen()
        {
            State = NetClientState.Connected;
            OnOpen?.Invoke();
        }

        private void HandleTransportClose(ushort code, string reason)
        {
            State = NetClientState.Disconnected;
            OnClose?.Invoke(code, reason);
        }

        private void HandleTransportError(Exception ex)
        {
            if (State != NetClientState.Disconnected)
            {
                State = NetClientState.Faulted;
            }
            OnError?.Invoke(ex);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name);
        }
    }
}
