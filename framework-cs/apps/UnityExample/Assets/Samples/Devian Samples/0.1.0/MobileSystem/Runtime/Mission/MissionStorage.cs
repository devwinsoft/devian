using System;
using System.Collections.Generic;

namespace Devian
{
    [Serializable]
    public sealed class MissionStorage
    {
        public int schemaVersion = 2;
        public long dailyMissionStartUtcMs;
        public MissionClockSnapshot clockSnapshot = new();
        public long clockReceivedAtClientUtcMs;
        public int nextMissionUid = 1;
        public Dictionary<int, MissionRuntimeBase> runtimes = new();

        public void Clear()
        {
            schemaVersion = 2;
            dailyMissionStartUtcMs = 0L;
            clockSnapshot = new MissionClockSnapshot();
            clockReceivedAtClientUtcMs = 0L;
            nextMissionUid = 1;
            runtimes.Clear();
        }
    }
}
