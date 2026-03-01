#nullable enable
using System;
using System.Collections.Generic;

namespace Devian
{
    /// <summary>
    /// Thread-safe dispatch queue for main-thread callback execution.
    /// Enqueue from worker threads and drain from Tick().
    /// </summary>
    public sealed class NetDispatchQueue
    {
        private readonly object _lock = new();
        private readonly Queue<Action> _queue = new();
        private readonly Action<Exception>? _onDispatchError;

        public NetDispatchQueue(Action<Exception>? onDispatchError = null)
        {
            _onDispatchError = onDispatchError;
        }

        public void Enqueue(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            lock (_lock)
            {
                _queue.Enqueue(action);
            }
        }

        public int Drain(int maxItems)
        {
            if (maxItems <= 0)
                return 0;

            var processed = 0;

            while (processed < maxItems)
            {
                Action? action;
                lock (_lock)
                {
                    if (_queue.Count == 0)
                        return processed;

                    action = _queue.Dequeue();
                }

                processed++;

                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    ReportDispatchError(ex);
                }
            }

            return processed;
        }

        public void Clear()
        {
            lock (_lock)
            {
                _queue.Clear();
            }
        }

        private void ReportDispatchError(Exception ex)
        {
            if (_onDispatchError == null)
            {
                Log.Error($"[NetDispatchQueue] Dispatch callback failed: {ex}");
                return;
            }

            try
            {
                _onDispatchError(ex);
            }
            catch (Exception sinkEx)
            {
                Log.Error($"[NetDispatchQueue] Dispatch callback failed: {ex}");
                Log.Error($"[NetDispatchQueue] Dispatch error sink failed: {sinkEx}");
            }
        }
    }
}
