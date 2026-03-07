using System;
using System.Collections.Generic;

namespace Devian
{
    [Serializable]
    public sealed class AchieveStorage
    {
        public int schemaVersion = 1;
        public int nextAchieveUid = 1;
        public Dictionary<int, AchieveRuntime> runtimes = new();
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

            stats[messageId] = value;
        }

        public int AllocateAchieveUid()
        {
            if (nextAchieveUid <= 0)
                nextAchieveUid = 1;

            var candidate = nextAchieveUid;
            while (runtimes.ContainsKey(candidate))
                candidate++;

            nextAchieveUid = candidate + 1;
            return candidate;
        }

        public void Clear()
        {
            schemaVersion = 1;
            nextAchieveUid = 1;
            runtimes.Clear();
            stats.Clear();
        }
    }
}
