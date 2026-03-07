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
        }
    }
}
