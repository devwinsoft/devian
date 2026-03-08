using Newtonsoft.Json.Linq;

namespace Devian
{
    internal static class SaveDataJsonCodecRemoteConfig
    {
        public static JObject Serialize(RemoteConfigStorage storage)
        {
            storage ??= new RemoteConfigStorage();

            return new JObject
            {
                ["schemaVersion"] = storage.schemaVersion,
                ["snapshotReceivedAtClientUtcMs"] = storage.snapshotReceivedAtClientUtcMs,
                ["snapshot"] = SerializeSnapshot(storage.snapshot),
            };
        }

        public static void DeserializeInto(JObject remoteConfigObj, RemoteConfigStorage storage)
        {
            if (storage == null)
                return;

            storage.Clear();
            if (remoteConfigObj == null)
                return;

            storage.schemaVersion = remoteConfigObj.Value<int?>("schemaVersion") ?? 1;
            storage.snapshotReceivedAtClientUtcMs = remoteConfigObj.Value<long?>("snapshotReceivedAtClientUtcMs") ?? 0L;

            if (remoteConfigObj["snapshot"] is JObject snapshotObj)
                storage.snapshot = DeserializeSnapshot(snapshotObj);
            else
                storage.snapshot = new RemoteConfigSnapshot();
        }

        public static void MigrateFromLegacyMissionClock(JObject missionObj, RemoteConfigStorage storage)
        {
            if (missionObj == null || storage == null)
                return;

            storage.Clear();
            storage.schemaVersion = 1;
            storage.snapshotReceivedAtClientUtcMs = missionObj.Value<long?>("clockReceivedAtClientUtcMs") ?? 0L;

            if (missionObj["clockSnapshot"] is JObject clockObj)
                storage.snapshot = DeserializeSnapshot(clockObj);
            else
                storage.snapshot = new RemoteConfigSnapshot();
        }

        static JObject SerializeSnapshot(RemoteConfigSnapshot snapshot)
        {
            snapshot ??= new RemoteConfigSnapshot();
            return new JObject
            {
                ["serverNowUtcMs"] = snapshot.serverNowUtcMs,
                ["minVersion"] = snapshot.minVersion ?? string.Empty,
                ["currentVersion"] = snapshot.currentVersion ?? string.Empty,
            };
        }

        static RemoteConfigSnapshot DeserializeSnapshot(JObject snapshotObj)
        {
            var snapshot = new RemoteConfigSnapshot(snapshotObj.Value<long?>("serverNowUtcMs") ?? 0L)
            {
                minVersion = snapshotObj.Value<string>("minVersion") ?? string.Empty,
                currentVersion = snapshotObj.Value<string>("currentVersion") ?? string.Empty,
            };
            return snapshot;
        }
    }
}
