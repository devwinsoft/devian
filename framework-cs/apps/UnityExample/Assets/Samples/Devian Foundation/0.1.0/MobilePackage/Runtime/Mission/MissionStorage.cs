using System;
using System.Collections.Generic;

namespace Devian
{
    [Serializable]
    public sealed class MissionStorage
    {
        public int schemaVersion = 3;
        public long dailyMissionStartUtcMs;
        public long weeklyMissionStartUtcMs;
        public int nextMissionUid = 1;
        public Dictionary<int, MissionRuntimeBase> runtimes = new();

        public void Clear()
        {
            schemaVersion = 3;
            dailyMissionStartUtcMs = 0L;
            weeklyMissionStartUtcMs = 0L;
            nextMissionUid = 1;
            runtimes.Clear();
        }
    }
}
