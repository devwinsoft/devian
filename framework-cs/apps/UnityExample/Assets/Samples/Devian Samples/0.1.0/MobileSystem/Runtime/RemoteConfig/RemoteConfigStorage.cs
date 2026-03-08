using System;

namespace Devian
{
    [Serializable]
    public sealed class RemoteConfigStorage
    {
        public int schemaVersion = 1;
        public RemoteConfigSnapshot snapshot = new();
        public long snapshotReceivedAtClientUtcMs;

        public void Clear()
        {
            schemaVersion = 1;
            snapshot = new RemoteConfigSnapshot();
            snapshotReceivedAtClientUtcMs = 0L;
        }
    }
}
