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
        public Dictionary<string, CBigInt> stats = new(StringComparer.Ordinal);

        public bool TryGetStat(string missionStatId, out CBigInt value)
        {
            value = CBigInt.Zero;
            return !string.IsNullOrWhiteSpace(missionStatId)
                   && stats.TryGetValue(missionStatId, out value);
        }

        public CBigInt GetStat(string missionStatId)
        {
            return TryGetStat(missionStatId, out var value) ? value : CBigInt.Zero;
        }

        public void SetStat(string missionStatId, CBigInt value)
        {
            if (string.IsNullOrWhiteSpace(missionStatId))
                return;

            stats[missionStatId] = value;
        }

        public void Clear()
        {
            schemaVersion = 2;
            dailyMissionStartUtcMs = 0L;
            clockSnapshot = new MissionClockSnapshot();
            clockReceivedAtClientUtcMs = 0L;
            nextMissionUid = 1;
            runtimes.Clear();
            stats.Clear();
        }
    }
}
