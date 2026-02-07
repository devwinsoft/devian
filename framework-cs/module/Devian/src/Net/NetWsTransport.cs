#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Devian
{
    /// <summary>
    /// WebSocket transport adapter that implements INetTransport.
    /// Wraps NetWsClient for use with NetClientBase.
    /// </summary>
    public sealed class NetWsTransport : INetTransport
    {
        private readonly NetWsClient _ws;
        private TaskCompletionSource<bool>? _connectTcs;
        private TaskCompletionSource<bool>? _closeTcs;
        private bool _disposed;
        private bool _connectCompleted;
        private bool _closeCompleted;
        private bool _opened;

        /// <inheritdoc />
        public bool IsConnected => _ws.IsOpen;

        /// <inheritdoc />
        public event Action? OnOpen;

        /// <inheritdoc />
        public event Action<ushort, string>? OnClose;

        /// <inheritdoc />
        public event Action<Exception>? OnError;

        /// <summary>
        /// Creates a new WebSocket transport.
        /// </summary>
        /// <param name="core">NetClient for frame dispatch.</param>
        /// <param name="sessionId">Session identifier.</param>
        public NetWsTransport(NetClient core, int sessionId = 0)
        {
            _ws = new NetWsClient(core, sessionId);

            // Wire internal events
            _ws.OnOpen += HandleOpen;
            _ws.OnClose += HandleClose;
            _ws.OnError += HandleError;
        }

        /// <inheritdoc />
        public async Task ConnectAsync(string url, CancellationToken ct = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(NetWsTransport));

            // Start new attempt: reset attempt state
            _opened = false;
            _connectCompleted = false;
            _connectTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Connect (completion via events)
            _ws.Connect(url);

            // If completed synchronously (Editor/Standalone), return immediately
            if (_connectCompleted)
                return;

            using var reg = ct.Register(() =>
            {
                if (!_connectCompleted)
                    _connectTcs.TrySetCanceled(ct);
            });

            await _connectTcs.Task.ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task CloseAsync(CancellationToken ct = default)
        {
            if (_disposed || !_ws.IsOpen)
                return;

            _closeCompleted = false;
            _closeTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            _ws.Close();

            if (_closeCompleted)
                return;

            using var reg = ct.Register(() =>
            {
                if (!_closeCompleted)
                    _closeTcs.TrySetCanceled(ct);
            });

            await _closeTcs.Task.ConfigureAwait(false);
        }

        /// <inheritdoc />
        public void Tick()
        {
            if (!_disposed)
                _ws.Tick();
        }

        /// <inheritdoc />
        public void SendFrame(ReadOnlySpan<byte> frame)
        {
            if (!_disposed && _ws.IsOpen)
                _ws.SendFrame(frame);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _ws.OnOpen -= HandleOpen;
            _ws.OnClose -= HandleClose;
            _ws.OnError -= HandleError;

            _ws.Dispose();

            // Complete any pending tasks
            _connectTcs?.TrySetCanceled();
            _closeTcs?.TrySetCanceled();
        }

        private void HandleOpen()
        {
            _opened = true;
            _connectCompleted = true;
            _connectTcs?.TrySetResult(true);
            OnOpen?.Invoke();
        }

        private void HandleClose(ushort code, string reason)
        {
            _closeCompleted = true;
            _closeTcs?.TrySetResult(true);

            if (!_connectCompleted)
            {
                _connectCompleted = true;
                _connectTcs?.TrySetException(new Exception($"Closed during connect: {code} {reason}"));
            }

            OnClose?.Invoke(code, reason);
        }

        private void HandleError(Exception ex)
        {
            _connectCompleted = true;
            _connectTcs?.TrySetException(ex);

            // Option A: during connecting phase (before OnOpen), do NOT propagate OnError event.
            // The failure is delivered via await ConnectAsync only.
            // After already opened, OnError represents runtime transport errors.
            if (_opened)
                OnError?.Invoke(ex);
        }
    }
}
