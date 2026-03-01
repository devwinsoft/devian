#nullable enable
using System;

namespace Devian
{
    /// <summary>
    /// Delegate for handling unhandled frames.
    /// Note: payload is a Span and cannot be stored. Copy if needed.
    /// </summary>
    public delegate void NetUnhandledFrameHandler(int sessionId, int opcode, ReadOnlySpan<byte> payload);

    /// <summary>
    /// Core client logic for frame reception and dispatch routing.
    /// Used by transport implementations (e.g., NetWsClient).
    /// </summary>
    public sealed class NetClient
    {
        private readonly NetInboundDispatcher _dispatcher;

        /// <summary>
        /// Optional callback for unhandled messages.
        /// Note: payload is a Span (cannot be stored). Copy if needed.
        /// </summary>
        public NetUnhandledFrameHandler? OnUnhandled
        {
            get => _dispatcher.OnUnhandled;
            set => _dispatcher.OnUnhandled = value;
        }

        /// <summary>
        /// Optional callback for parse errors.
        /// </summary>
        public Action<int, Exception>? OnParseError
        {
            get => _dispatcher.OnParseError;
            set => _dispatcher.OnParseError = value;
        }

        /// <summary>
        /// Optional callback for runtime dispatch errors.
        /// Fired when protocol handler code throws during inbound dispatch.
        /// </summary>
        public Action<int, int, Exception>? OnDispatchError
        {
            get => _dispatcher.OnDispatchError;
            set => _dispatcher.OnDispatchError = value;
        }

        /// <summary>
        /// Creates a new NetClient with the specified runtime.
        /// </summary>
        /// <param name="runtime">The runtime that handles message dispatch.</param>
        public NetClient(INetRuntime runtime)
        {
            _dispatcher = new NetInboundDispatcher(runtime);
        }

        /// <summary>
        /// Called by transport when a complete frame is received.
        /// Parses the frame and dispatches to the runtime.
        /// Does not throw; errors are reported via OnParseError callback.
        /// </summary>
        /// <param name="sessionId">Session identifier.</param>
        /// <param name="frame">Complete frame bytes.</param>
        public void OnFrame(int sessionId, ReadOnlySpan<byte> frame)
        {
            _dispatcher.DispatchFrame(sessionId, frame);
        }
    }
}
