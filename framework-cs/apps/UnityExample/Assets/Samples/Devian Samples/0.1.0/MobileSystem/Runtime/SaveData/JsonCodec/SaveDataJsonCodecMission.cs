using Newtonsoft.Json.Linq;
using Devian.Domain.Game;

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
                ["periodMissionStartUtcMs"] = storage.periodMissionStartUtcMs,
                ["nextMissionUid"] = storage.nextMissionUid,
            };

            var runtimes = new JArray();
            foreach (var runtime in storage.runtimes.Values)
            {
                JObject runtimeObj = null;
                switch (runtime)
                {
                    case MissionRuntimeDaily dailyRuntime:
                        runtimeObj = new JObject
                        {
                            ["missionType"] = (int)MISSION_TYPE.DAILY,
                            ["missionId"] = dailyRuntime.missionId,
                            ["messageId"] = dailyRuntime.messageId,
                            ["missionUid"] = dailyRuntime.missionUid,
                            ["periodKey"] = dailyRuntime.periodKey,
                            ["index"] = dailyRuntime.index,
                            ["progressValue"] = SerializeBigInt(dailyRuntime.progressValue),
                            ["isCompleted"] = dailyRuntime.isCompleted,
                        };
                        break;

                    case MissionRuntimePeriod periodRuntime:
                        runtimeObj = new JObject
                        {
                            ["missionType"] = (int)MISSION_TYPE.PERIOD,
                            ["missionId"] = periodRuntime.missionId,
                            ["messageId"] = periodRuntime.messageId,
                            ["missionUid"] = periodRuntime.missionUid,
                            ["periodKey"] = periodRuntime.periodKey,
                            ["day"] = periodRuntime.day,
                            ["isWaiting"] = periodRuntime.isWaiting,
                            ["progressValue"] = SerializeBigInt(periodRuntime.progressValue),
                            ["isCompleted"] = periodRuntime.isCompleted,
                        };
                        break;
                }

                if (runtimeObj != null)
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

            storage.schemaVersion = missionObj.Value<int?>("schemaVersion") ?? 3;
            storage.dailyMissionStartUtcMs = missionObj.Value<long?>("dailyMissionStartUtcMs") ?? 0L;
            storage.periodMissionStartUtcMs = missionObj.Value<long?>("periodMissionStartUtcMs") ?? 0L;
            storage.nextMissionUid = missionObj.Value<int?>("nextMissionUid") ?? 1;

            if (storage.periodMissionStartUtcMs <= 0L)
                storage.periodMissionStartUtcMs = storage.dailyMissionStartUtcMs;

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

                    var missionUid = runtimeObj.Value<int?>("missionUid") ?? 0;
                    if (missionUid <= 0)
                        continue;

                    var missionType = MISSION_TYPE.DAILY;
                    var missionTypeRaw = runtimeObj.Value<int?>("missionType");
                    if (missionTypeRaw.HasValue)
                    {
                        if (!System.Enum.IsDefined(typeof(MISSION_TYPE), missionTypeRaw.Value))
                            continue;

                        missionType = (MISSION_TYPE)missionTypeRaw.Value;
                    }

                    var missionId = runtimeObj.Value<string>("missionId") ?? string.Empty;
                    var messageId = runtimeObj.Value<string>("messageId")
                                    ?? runtimeObj.Value<string>("missionStatId")
                                    ?? string.Empty;
                    var periodKey = runtimeObj.Value<string>("periodKey") ?? string.Empty;
                    var progressValue = DeserializeBigInt(runtimeObj["progressValue"]);
                    var isCompleted = runtimeObj.Value<bool?>("isCompleted") ?? false;

                    MissionRuntimeBase runtime;
                    switch (missionType)
                    {
                        case MISSION_TYPE.PERIOD:
                            runtime = new MissionRuntimePeriod
                            {
                                missionId = missionId,
                                messageId = messageId,
                                periodKey = periodKey,
                                missionUid = missionUid,
                                day = System.Math.Clamp(runtimeObj.Value<int?>("day") ?? 1, 1, 7),
                                isWaiting = (runtimeObj.Value<bool?>("isWaiting") ?? false) && !isCompleted,
                                progressValue = progressValue,
                                isCompleted = isCompleted,
                            };
                            break;

                        default:
                            runtime = new MissionRuntimeDaily
                            {
                                missionId = missionId,
                                messageId = messageId,
                                periodKey = periodKey,
                                missionUid = missionUid,
                                index = runtimeObj.Value<int?>("index") ?? 0,
                                progressValue = progressValue,
                                isWaiting = false,
                                isCompleted = isCompleted,
                            };
                            break;
                    }

                    storage.runtimes[runtime.missionUid] = runtime;
                }
            }

            if (storage.nextMissionUid <= 0)
                storage.nextMissionUid = 1;
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
