using System;

namespace Devian
{
    [Serializable]
    public sealed class MissionClockSnapshot
    {
        public long serverNowUtcMs;

        public MissionClockSnapshot(long serverNowUtcMs = 0L)
        {
            this.serverNowUtcMs = serverNowUtcMs;
        }

        public void Clear()
        {
            serverNowUtcMs = 0L;
        }
    }
}
