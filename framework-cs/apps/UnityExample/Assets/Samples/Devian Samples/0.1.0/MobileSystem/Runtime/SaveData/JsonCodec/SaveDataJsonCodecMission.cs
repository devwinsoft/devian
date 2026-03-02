using Devian.Domain.Game;
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
                if (runtime == null)
                    continue;

                var runtimeObj = new JObject
                {
                    ["missionType"] = (int)runtime.missionType,
                    ["missionId"] = runtime.missionId,
                    ["periodKey"] = runtime.periodKey,
                    ["missionUid"] = runtime.missionUid,
                    ["progressValue"] = SerializeBigInt(runtime.progressValue),
                    ["isCompleted"] = runtime.isCompleted,
                    ["rewardGroupId"] = runtime.rewardGroupId,
                };

                if (runtime is MissionRuntimeAchieve achieveRuntime)
                {
                    runtimeObj["level"] = achieveRuntime.level;
                    runtimeObj["startValue"] = SerializeBigInt(achieveRuntime.startValue);
                }
                else if (runtime is MissionRuntimeDaily dailyRuntime)
                {
                    runtimeObj["index"] = dailyRuntime.index;
                }

                runtimes.Add(runtimeObj);
            }

            missionObj["runtimes"] = runtimes;

            var claimRecords = new JArray();
            foreach (var claimRecord in storage.claimRecords.Values)
            {
                if (claimRecord == null)
                    continue;

                claimRecords.Add(new JObject
                {
                    ["missionType"] = (int)claimRecord.missionType,
                    ["missionId"] = claimRecord.missionId,
                    ["level"] = claimRecord.level,
                    ["grantId"] = claimRecord.grantId,
                    ["periodKey"] = claimRecord.periodKey,
                    ["rewardGroupId"] = claimRecord.rewardGroupId,
                    ["claimedAtClientUtcMs"] = claimRecord.claimedAtClientUtcMs,
                });
            }

            missionObj["claimRecords"] = claimRecords;
            return missionObj;
        }

        public static void DeserializeInto(JObject missionObj, MissionStorage storage)
        {
            if (storage == null)
                return;

            storage.Clear();

            if (missionObj == null)
                return;

            storage.schemaVersion = missionObj.Value<int?>("schemaVersion") ?? 1;
            storage.dailyMissionStartUtcMs = missionObj.Value<long?>("dailyMissionStartUtcMs") ?? 0L;
            storage.clockReceivedAtClientUtcMs = missionObj.Value<long?>("clockReceivedAtClientUtcMs") ?? 0L;
            storage.nextMissionUid = missionObj.Value<int?>("nextMissionUid") ?? 1;

            if (missionObj["clockSnapshot"] is JObject clockObj)
                storage.clockSnapshot = DeserializeClockSnapshot(clockObj);
            else
                storage.clockSnapshot = new MissionClockSnapshot();

            if (missionObj["runtimes"] is JArray runtimeArray)
            {
                foreach (var token in runtimeArray)
                {
                    if (token is not JObject runtimeObj)
                        continue;

                    var missionTypeRaw = runtimeObj.Value<int?>("missionType")
                        ?? runtimeObj.Value<int?>("missionKind")
                        ?? (int)MISSION_TYPE.DAY;
                    if (!System.Enum.IsDefined(typeof(MISSION_TYPE), missionTypeRaw))
                        continue;

                    var missionType = (MISSION_TYPE)missionTypeRaw;
                    var missionUid = runtimeObj.Value<int?>("missionUid") ?? 0;
                    if (missionUid <= 0)
                        continue;

                    MissionRuntimeBase runtime;
                    switch (missionType)
                    {
                        case MISSION_TYPE.ACHIEVE:
                            runtime = new MissionRuntimeAchieve
                            {
                                missionType = missionType,
                                missionId = runtimeObj.Value<string>("missionId") ?? string.Empty,
                                periodKey = runtimeObj.Value<string>("periodKey") ?? string.Empty,
                                missionUid = missionUid,
                                progressValue = DeserializeBigInt(runtimeObj["progressValue"]),
                                isCompleted = runtimeObj.Value<bool?>("isCompleted") ?? false,
                                rewardGroupId = runtimeObj.Value<string>("rewardGroupId") ?? string.Empty,
                                level = runtimeObj.Value<int?>("level") ?? 1,
                                startValue = DeserializeBigInt(runtimeObj["startValue"]),
                            };
                            break;

                        case MISSION_TYPE.DAY:
                        default:
                            runtime = new MissionRuntimeDaily
                            {
                                missionType = missionType,
                                missionId = runtimeObj.Value<string>("missionId") ?? string.Empty,
                                periodKey = runtimeObj.Value<string>("periodKey") ?? string.Empty,
                                missionUid = missionUid,
                                index = runtimeObj.Value<int?>("index") ?? 0,
                                progressValue = DeserializeBigInt(runtimeObj["progressValue"]),
                                isCompleted = runtimeObj.Value<bool?>("isCompleted") ?? false,
                                rewardGroupId = runtimeObj.Value<string>("rewardGroupId") ?? string.Empty,
                            };
                            break;
                    }

                    storage.runtimes[runtime.missionUid] = runtime;
                }
            }

            if (missionObj["claimRecords"] is JArray claimRecordArray)
            {
                foreach (var token in claimRecordArray)
                {
                    if (token is not JObject claimObj)
                        continue;

                    var missionTypeRaw = claimObj.Value<int?>("missionType")
                        ?? claimObj.Value<int?>("missionKind")
                        ?? (int)MISSION_TYPE.DAY;
                    if (!System.Enum.IsDefined(typeof(MISSION_TYPE), missionTypeRaw))
                        continue;

                    var record = new MissionClaimRecord
                    {
                        missionType = (MISSION_TYPE)missionTypeRaw,
                        missionId = claimObj.Value<string>("missionId") ?? string.Empty,
                        level = claimObj.Value<int?>("level") ?? 1,
                        grantId = claimObj.Value<string>("grantId") ?? string.Empty,
                        periodKey = claimObj.Value<string>("periodKey") ?? string.Empty,
                        rewardGroupId = claimObj.Value<string>("rewardGroupId") ?? string.Empty,
                        claimedAtClientUtcMs = claimObj.Value<long?>("claimedAtClientUtcMs") ?? 0L,
                    };

                    if (string.IsNullOrWhiteSpace(record.grantId))
                        continue;

                    storage.claimRecords[record.grantId] = record;
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
