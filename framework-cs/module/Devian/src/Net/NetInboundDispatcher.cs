#nullable enable
using System;

namespace Devian
{
    /// <summary>
    /// Parses inbound frames and routes them to the protocol runtime.
    /// Transport-level socket code should not own protocol dispatch policy.
    /// </summary>
    public sealed class NetInboundDispatcher
    {
        private readonly INetRuntime _runtime;

        public NetUnhandledFrameHandler? OnUnhandled { get; set; }

        public Action<int, Exception>? OnParseError { get; set; }

        public Action<int, int, Exception>? OnDispatchError { get; set; }

        public NetInboundDispatcher(INetRuntime runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public void DispatchFrame(int sessionId, ReadOnlySpan<byte> frame)
        {
            int opcode;
            ReadOnlySpan<byte> payload;

            try
            {
                if (!NetFrameV1.TryParse(frame, out opcode, out payload))
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

            bool handled;
            try
            {
                handled = _runtime.TryDispatchInbound(sessionId, opcode, payload);
            }
            catch (Exception ex)
            {
                ReportDispatchError(sessionId, opcode, ex);
                return;
            }

            if (!handled)
            {
                OnUnhandled?.Invoke(sessionId, opcode, payload);
            }
        }

        private void ReportDispatchError(int sessionId, int opcode, Exception ex)
        {
            Log.Error($"[NetInboundDispatcher] Runtime dispatch failed: sessionId={sessionId}, opcode={opcode}, ex={ex}");

            if (OnDispatchError == null)
                return;

            try
            {
                OnDispatchError(sessionId, opcode, ex);
            }
            catch (Exception callbackEx)
            {
                Log.Error($"[NetInboundDispatcher] OnDispatchError handler failed: {callbackEx}");
            }
        }
    }
}
