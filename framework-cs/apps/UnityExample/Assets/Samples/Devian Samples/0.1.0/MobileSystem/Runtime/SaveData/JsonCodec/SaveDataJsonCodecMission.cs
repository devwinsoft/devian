using Newtonsoft.Json.Linq;

namespace Devian
{
    internal static class SaveDataJsonCodecMission
    {
        public static JObject Serialize(MissionStorage storage)
        {
            storage ??= new MissionStorage();

            var missionObj = new JObject
            {
                ["schemaVersion"] = storage.schemaVersion,
                ["dailyMissionStartUtcMs"] = storage.dailyMissionStartUtcMs,
                ["clockReceivedAtClientUtcMs"] = storage.clockReceivedAtClientUtcMs,
                ["nextMissionUid"] = storage.nextMissionUid,
                ["clockSnapshot"] = SerializeClockSnapshot(storage.clockSnapshot),
            };

            var runtimes = new JArray();
            foreach (var runtime in storage.runtimes.Values)
            {
                if (runtime is not MissionRuntimeDaily dailyRuntime)
                    continue;

                var runtimeObj = new JObject
                {
                    ["missionId"] = dailyRuntime.missionId,
                    ["messageId"] = dailyRuntime.messageId,
                    ["missionUid"] = dailyRuntime.missionUid,
                    ["periodKey"] = dailyRuntime.periodKey,
                    ["index"] = dailyRuntime.index,
                    ["progressValue"] = SerializeBigInt(dailyRuntime.progressValue),
                    ["isCompleted"] = dailyRuntime.isCompleted,
                };

                runtimes.Add(runtimeObj);
            }

            missionObj["runtimes"] = runtimes;
            return missionObj;
        }

        public static void DeserializeInto(JObject missionObj, MissionStorage storage, GameMessageStorage messageStorage)
        {
            if (storage == null)
                return;

            storage.Clear();

            if (missionObj == null)
                return;

            storage.schemaVersion = missionObj.Value<int?>("schemaVersion") ?? 2;
            storage.dailyMissionStartUtcMs = missionObj.Value<long?>("dailyMissionStartUtcMs") ?? 0L;
            storage.clockReceivedAtClientUtcMs = missionObj.Value<long?>("clockReceivedAtClientUtcMs") ?? 0L;
            storage.nextMissionUid = missionObj.Value<int?>("nextMissionUid") ?? 1;

            if (missionObj["clockSnapshot"] is JObject clockObj)
                storage.clockSnapshot = DeserializeClockSnapshot(clockObj);
            else
                storage.clockSnapshot = new MissionClockSnapshot();

            // v12 migration: move mission.stats -> message.stats
            if (missionObj["stats"] is JObject statsObj && messageStorage != null)
            {
                foreach (var property in statsObj.Properties())
                {
                    if (string.IsNullOrWhiteSpace(property.Name))
                        continue;

                    messageStorage.SetStat(property.Name, DeserializeBigInt(property.Value));
                }
            }

            if (missionObj["runtimes"] is JArray runtimeArray)
            {
                foreach (var token in runtimeArray)
                {
                    if (token is not JObject runtimeObj)
                        continue;

                    var missionTypeRaw = runtimeObj.Value<int?>("missionType");
                    if (missionTypeRaw.HasValue)
                    {
                        if (!System.Enum.IsDefined(typeof(Devian.Domain.Game.MISSION_TYPE), missionTypeRaw.Value))
                            continue;

                        if ((Devian.Domain.Game.MISSION_TYPE)missionTypeRaw.Value != Devian.Domain.Game.MISSION_TYPE.DAY)
                            continue;
                    }

                    var missionUid = runtimeObj.Value<int?>("missionUid") ?? 0;
                    if (missionUid <= 0)
                        continue;

                    var runtime = new MissionRuntimeDaily
                    {
                        missionId = runtimeObj.Value<string>("missionId") ?? string.Empty,
                        messageId = runtimeObj.Value<string>("messageId")
                                    ?? runtimeObj.Value<string>("missionStatId")
                                    ?? string.Empty,
                        periodKey = runtimeObj.Value<string>("periodKey") ?? string.Empty,
                        missionUid = missionUid,
                        index = runtimeObj.Value<int?>("index") ?? 0,
                        progressValue = DeserializeBigInt(runtimeObj["progressValue"]),
                        isCompleted = runtimeObj.Value<bool?>("isCompleted") ?? false,
                    };

                    storage.runtimes[runtime.missionUid] = runtime;
                }
            }

            if (storage.nextMissionUid <= 0)
                storage.nextMissionUid = 1;
        }

        static JObject SerializeClockSnapshot(MissionClockSnapshot snapshot)
        {
            snapshot ??= new MissionClockSnapshot();
            return new JObject
            {
                ["serverNowUtcMs"] = snapshot.serverNowUtcMs,
            };
        }

        static MissionClockSnapshot DeserializeClockSnapshot(JObject clockObj)
        {
            return new MissionClockSnapshot(clockObj.Value<long?>("serverNowUtcMs") ?? 0L);
        }

        static JObject SerializeBigInt(CBigInt value)
        {
            return new JObject
            {
                ["base"] = (float)value.mBase,
                ["pow"] = (int)value.mPow,
            };
        }

        static CBigInt DeserializeBigInt(JToken token)
        {
            if (token is not JObject valueObj)
                return CBigInt.Zero;

            var @base = valueObj.Value<float?>("base") ?? 0f;
            var pow = valueObj.Value<int?>("pow") ?? 0;
            return new CBigInt(@base, pow);
        }
    }
}
