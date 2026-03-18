using System;
using System.Collections.Generic;

namespace Devian
{
    [Serializable]
    public sealed class GameMessageStorage
    {
        public int schemaVersion = 1;
        public Dictionary<string, CBigInt> stats = new(StringComparer.Ordinal);

        public bool TryGetStat(string messageId, out CBigInt value)
        {
            value = CBigInt.Zero;
            return !string.IsNullOrWhiteSpace(messageId)
                   && stats.TryGetValue(messageId, out value);
        }

        public CBigInt GetStat(string messageId)
        {
            return TryGetStat(messageId, out var value) ? value : CBigInt.Zero;
        }

        public void SetStat(string messageId, CBigInt value)
        {
            if (string.IsNullOrWhiteSpace(messageId))
                return;

            stats[messageId] = GameMessageRule.ClampNonNegative(value);
        }

        public void Clear()
        {
            schemaVersion = 1;
            stats.Clear();
        }
    }
}
