using System;
using System.Collections.Generic;

namespace Devian
{
    /// <summary>
    /// Dictionary 기반 InputFrame 발행/구독 버스.
    /// Publish 시 key snapshot으로 re-entrancy safe.
    /// </summary>
    public class InputBus : IInputBus
    {
        private readonly Dictionary<int, Action<InputFrame>> _handlers = new();
        private int _nextToken;

        // Snapshot buffer for safe iteration during Publish
        private readonly List<int> _snapshotKeys = new();

        public void Publish(InputFrame frame)
        {
            // Snapshot keys to avoid collection-modified issues
            _snapshotKeys.Clear();
            foreach (var kvp in _handlers)
            {
                _snapshotKeys.Add(kvp.Key);
            }

            for (int i = 0; i < _snapshotKeys.Count; i++)
            {
                int key = _snapshotKeys[i];
                if (_handlers.TryGetValue(key, out var handler))
                {
                    handler(frame);
                }
            }
        }

        public int Subscribe(Action<InputFrame> handler)
        {
            int token = _nextToken++;
            _handlers[token] = handler;
            return token;
        }

        public void Unsubscribe(int token)
        {
            _handlers.Remove(token);
        }
    }
}
