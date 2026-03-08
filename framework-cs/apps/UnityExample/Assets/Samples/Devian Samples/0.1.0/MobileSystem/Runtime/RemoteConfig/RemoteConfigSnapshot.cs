using System;

namespace Devian
{
    [Serializable]
    public sealed class RemoteConfigSnapshot
    {
        public long serverNowUtcMs;
        public string minVersion = string.Empty;
        public string currentVersion = string.Empty;

        public RemoteConfigSnapshot(long serverNowUtcMs = 0L)
        {
            this.serverNowUtcMs = serverNowUtcMs;
            minVersion = string.Empty;
            currentVersion = string.Empty;
        }

        public void Clear()
        {
            serverNowUtcMs = 0L;
            minVersion = string.Empty;
            currentVersion = string.Empty;
        }
    }
}
