using System;
using System.Collections.Generic;

namespace Devian
{
    [Serializable]
    public sealed class AchieveStorage
    {
        public int schemaVersion = 2;
        public int nextAchieveUid = 1;
        public Dictionary<int, AchieveRuntimeBase> runtimes = new();

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
            schemaVersion = 2;
            nextAchieveUid = 1;
            runtimes.Clear();
        }
    }
}
