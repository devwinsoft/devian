#nullable enable
using System;

namespace Devian.Network
{
    /// <summary>
    /// Delegate for handling unhandled frames.
    /// Note: payload is a Span and cannot be stored. Copy if needed.
    /// </summary>
    public delegate void UnhandledFrameHandler(int sessionId, int opcode, ReadOnlySpan<byte> payload);

    /// <summary>
    /// Core client logic for frame reception and dispatch routing.
    /// Used by transport implementations (e.g., WebSocketClient).
    /// </summary>
    public sealed class NetworkClient
    {
        private readonly INetRuntime _runtime;

        /// <summary>
        /// Optional callback for unhandled messages.
        /// Note: payload is a Span (cannot be stored). Copy if needed.
        /// </summary>
        public UnhandledFrameHandler? OnUnhandled { get; set; }

        /// <summary>
        /// Optional callback for parse errors.
        /// </summary>
        public Action<int, Exception>? OnParseError { get; set; }

        /// <summary>
        /// Creates a new NetworkClient with the specified runtime.
        /// </summary>
        /// <param name="runtime">The runtime that handles message dispatch.</param>
        public NetworkClient(INetRuntime runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
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
            int opcode;
            ReadOnlySpan<byte> payload;

            try
            {
                if (!FrameV1.TryParse(frame, out opcode, out payload))
                {
                    OnParseError?.Invoke(sessionId, new InvalidOperationException("Frame too short to parse"));
                    return;
                }
            }
            catch (Exception ex)
            {
                OnParseError?.Invoke(sessionId, ex);
                return;
            }

            var handled = false;
            try
            {
                handled = _runtime.TryDispatchInbound(sessionId, opcode, payload);
            }
            catch
            {
                // Runtime handler exception is swallowed to prevent transport crash.
                // In production, consider logging.
                handled = true; // Treat exception as "handled" to avoid OnUnhandled noise.
            }

            if (!handled)
            {
                // Pass payload span directly (no copy). Callback must copy if storage needed.
                OnUnhandled?.Invoke(sessionId, opcode, payload);
            }
        }
    }
}
