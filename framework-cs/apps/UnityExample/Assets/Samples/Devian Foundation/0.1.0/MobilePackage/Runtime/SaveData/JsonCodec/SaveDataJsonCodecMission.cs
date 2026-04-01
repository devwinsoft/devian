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
                ["weeklyMissionStartUtcMs"] = storage.weeklyMissionStartUtcMs,
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
                            ["mission_id"] = dailyRuntime.missionId,
                            ["missionUid"] = dailyRuntime.missionUid,
                            ["periodKey"] = dailyRuntime.periodKey,
                            ["index"] = dailyRuntime.index,
                            ["state"] = (int)dailyRuntime.state,
                            ["progressValue"] = SerializeBigInt(dailyRuntime.progressValue),
                        };
                        break;

                    case MissionRuntimeWeekly periodRuntime:
                        runtimeObj = new JObject
                        {
                            ["missionType"] = (int)MISSION_TYPE.WEEKLY,
                            ["mission_id"] = periodRuntime.missionId,
                            ["missionUid"] = periodRuntime.missionUid,
                            ["periodKey"] = periodRuntime.periodKey,
                            ["day"] = periodRuntime.day,
                            ["state"] = (int)periodRuntime.state,
                            ["progressValue"] = SerializeBigInt(periodRuntime.progressValue),
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
            storage.weeklyMissionStartUtcMs = missionObj.Value<long?>("weeklyMissionStartUtcMs") ?? 0L;
            storage.nextMissionUid = missionObj.Value<int?>("nextMissionUid") ?? 1;

            if (storage.weeklyMissionStartUtcMs <= 0L)
                storage.weeklyMissionStartUtcMs = storage.dailyMissionStartUtcMs;

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

                    var missionId = runtimeObj.Value<string>("mission_id") ?? string.Empty;
                    var periodKey = runtimeObj.Value<string>("periodKey") ?? string.Empty;
                    var progressValue = DeserializeBigInt(runtimeObj["progressValue"]);
                    var state = DeserializeState(runtimeObj.Value<int?>("state"));

                    MissionRuntimeBase runtime;
                    switch (missionType)
                    {
                        case MISSION_TYPE.WEEKLY:
                            runtime = new MissionRuntimeWeekly
                            {
                                missionId = missionId,
                                periodKey = periodKey,
                                missionUid = missionUid,
                                day = System.Math.Clamp(runtimeObj.Value<int?>("day") ?? 1, 1, 7),
                                state = state,
                                progressValue = progressValue,
                            };
                            break;

                        default:
                            runtime = new MissionRuntimeDaily
                            {
                                missionId = missionId,
                                periodKey = periodKey,
                                missionUid = missionUid,
                                index = runtimeObj.Value<int?>("index") ?? 0,
                                state = state,
                                progressValue = progressValue,
                            };
                            break;
                    }

                    storage.runtimes[runtime.missionUid] = runtime;
                }
            }

            if (storage.nextMissionUid <= 0)
                storage.nextMissionUid = 1;
        }

        static MissionRuntimeState DeserializeState(int? raw)
        {
            if (!raw.HasValue || !System.Enum.IsDefined(typeof(MissionRuntimeState), raw.Value))
                return MissionRuntimeState.ACTIVE;

            var state = (MissionRuntimeState)raw.Value;
            return state == MissionRuntimeState.NONE || state == MissionRuntimeState.CLAIMABLE
                ? MissionRuntimeState.ACTIVE
                : state;
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
