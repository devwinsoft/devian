#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Devian
{
    /// <summary>
    /// Reusable lifecycle owner for one client session and one or more outbound bindings.
    /// Protocol-specific runtime assembly is supplied by the caller.
    /// </summary>
    public class NetClientSessionHost : INetTickable, IDisposable
    {
        private readonly Func<INetRuntime> _runtimeFactory;
        private readonly INetSessionBindable[] _bindings;
        private readonly INetConnector _connector;

        private INetSession? _session;
        private Action? _sessionOpenHandler;
        private Action<ushort, string>? _sessionCloseHandler;
        private Action<Exception>? _sessionErrorHandler;
        private string _url = string.Empty;
        private string _lastError = string.Empty;
        private bool _disposed;
        private bool _isConnecting;
        private bool _errorNotified;

        public bool IsConnected => _session?.State == NetClientState.Connected;
        public bool IsConnecting => _isConnecting;
        public string Url => _url;
        public string LastError => _lastError;

        public event Action? OnOpen;
        public event Action<ushort, string>? OnClose;
        public event Action<Exception>? OnError;

        public NetClientSessionHost(
            Func<INetRuntime> runtimeFactory,
            IReadOnlyList<INetSessionBindable> bindings,
            INetConnector? connector = null)
        {
            _runtimeFactory = runtimeFactory ?? throw new ArgumentNullException(nameof(runtimeFactory));
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));

            _bindings = new INetSessionBindable[bindings.Count];
            for (var i = 0; i < bindings.Count; i++)
                _bindings[i] = bindings[i] ?? throw new ArgumentNullException(nameof(bindings));

            _connector = connector ?? new NetWsConnector();
        }

        public void Connect(string url)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(url))
                throw new ArgumentException("url is empty", nameof(url));

            ReleaseSession(disposeSession: true, detachBindings: true);

            _url = url;
            _lastError = string.Empty;
            _isConnecting = true;
            _errorNotified = false;

            var runtime = _runtimeFactory();
            var session = _connector.CreateSession(runtime, url);
            _sessionOpenHandler = () => HandleOpen(session);
            _sessionCloseHandler = (code, reason) => HandleClose(session, code, reason);
            _sessionErrorHandler = ex => HandleError(session, ex);

            session.OnOpen += _sessionOpenHandler;
            session.OnClose += _sessionCloseHandler;
            session.OnError += _sessionErrorHandler;

            _session = session;
            for (var i = 0; i < _bindings.Length; i++)
                _bindings[i].AttachSession(session);

            _ = session.ConnectAsync().ContinueWith(
                t => { var _ = t.Exception; },
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        }

        public void Tick()
        {
            if (_disposed)
                return;

            _session?.Tick();
        }

        public void Disconnect()
        {
            if (_disposed || _session == null)
                return;

            _ = _session.CloseAsync();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            ReleaseSession(disposeSession: true, detachBindings: true);
            _isConnecting = false;
            _errorNotified = false;
        }

        private void HandleOpen(INetSession session)
        {
            if (!ReferenceEquals(_session, session) || _disposed)
                return;

            _isConnecting = false;
            _errorNotified = false;
            OnOpen?.Invoke();
        }

        private void HandleClose(INetSession session, ushort code, string reason)
        {
            if (!ReferenceEquals(_session, session))
                return;

            _isConnecting = false;
            ReleaseSession(disposeSession: true, detachBindings: true);
            OnClose?.Invoke(code, reason);
        }

        private void HandleError(INetSession session, Exception ex)
        {
            if (!ReferenceEquals(_session, session))
                return;

            _isConnecting = false;
            _lastError = ex.Message;

            if (_errorNotified)
                return;

            _errorNotified = true;
            OnError?.Invoke(ex);

            if (_session?.State == NetClientState.Faulted)
                ReleaseSession(disposeSession: true, detachBindings: true);
        }

        private void ReleaseSession(bool disposeSession, bool detachBindings)
        {
            var session = _session;

            if (detachBindings)
            {
                for (var i = 0; i < _bindings.Length; i++)
                    _bindings[i].DetachSession();
            }

            if (session != null)
            {
                if (_sessionOpenHandler != null)
                    session.OnOpen -= _sessionOpenHandler;
                if (_sessionCloseHandler != null)
                    session.OnClose -= _sessionCloseHandler;
                if (_sessionErrorHandler != null)
                    session.OnError -= _sessionErrorHandler;

                if (disposeSession && session is IDisposable disposable)
                    disposable.Dispose();
            }

            _session = null;
            _sessionOpenHandler = null;
            _sessionCloseHandler = null;
            _sessionErrorHandler = null;
            _isConnecting = false;
            _errorNotified = false;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(NetClientSessionHost));
        }
    }
}
