using Newtonsoft.Json.Linq;
using Devian.Domain.Game;

namespace Devian
{
    internal static class SaveDataJsonCodecAchieve
    {
        public static JObject Serialize(AchieveStorage storage)
        {
            storage ??= new AchieveStorage();

            var achieveObj = new JObject
            {
                ["schemaVersion"] = storage.schemaVersion,
                ["nextAchieveUid"] = storage.nextAchieveUid,
            };

            var runtimes = new JArray();
            foreach (var runtime in storage.runtimes.Values)
            {
                if (runtime == null)
                    continue;

                var runtimeObj = new JObject
                {
                    ["achieveType"] = (int)runtime.RuntimeType,
                    ["achieveId"] = runtime.achieveId,
                    ["achieveUid"] = runtime.achieveUid,
                    ["level"] = runtime.level,
                    ["index"] = runtime.index,
                    ["progressValue"] = SerializeBigInt(runtime.progressValue),
                    ["state"] = (int)runtime.state,
                };

                runtimes.Add(runtimeObj);
            }

            achieveObj["runtimes"] = runtimes;
            return achieveObj;
        }

        public static void DeserializeInto(JObject achieveObj, AchieveStorage storage)
        {
            if (storage == null)
                return;

            storage.Clear();

            if (achieveObj == null)
                return;

            storage.schemaVersion = achieveObj.Value<int?>("schemaVersion") ?? 1;
            storage.nextAchieveUid = achieveObj.Value<int?>("nextAchieveUid") ?? 1;

            if (achieveObj["runtimes"] is JArray runtimeArray)
            {
                foreach (var token in runtimeArray)
                {
                    if (token is not JObject runtimeObj)
                        continue;

                    var achieveUid = runtimeObj.Value<int?>("achieveUid") ?? 0;
                    if (achieveUid <= 0)
                        continue;

                    var achieveId = runtimeObj.Value<string>("achieveId") ?? string.Empty;
                    var achieveType = parseAchieveType(runtimeObj.Value<int?>("achieveType"), achieveId);
                    var runtime = AchieveRuntimeFactory.Restore(new AchieveRuntimeRestoreArgs
                    {
                        AchieveType = achieveType,
                        AchieveId = achieveId,
                        AchieveUid = achieveUid,
                        Level = runtimeObj.Value<int?>("level") ?? 1,
                        Index = runtimeObj.Value<int?>("index") ?? 0,
                        ProgressValue = DeserializeBigInt(runtimeObj["progressValue"]),
                        State = deserializeAchieveState(runtimeObj),
                        StatType = GAME_MESSAGE_TYPE.NONE,
                        OpType = GAME_MESSAGE_SAVE_TYPE.NONE,
                        ConditionValue = CBigInt.Zero,
                        ReadProgress = null,
                        OnChanged = null,
                        OnClaimable = null,
                    });
                    if (runtime == null)
                        continue;

                    storage.runtimes[runtime.achieveUid] = runtime;
                }
            }

            if (storage.nextAchieveUid <= 0)
                storage.nextAchieveUid = 1;
        }

        static MissionRuntimeState deserializeAchieveState(JObject runtimeObj)
        {
            // New format: "state" int field
            var rawState = runtimeObj.Value<int?>("state");
            if (rawState.HasValue)
            {
                var s = (MissionRuntimeState)rawState.Value;
                return s == MissionRuntimeState.NONE || s == MissionRuntimeState.CLAIMABLE
                    ? MissionRuntimeState.ACTIVE
                    : s;
            }

            // Legacy format: isWaiting/isCompleted booleans → state conversion
            var isCompleted = runtimeObj.Value<bool?>("isCompleted") ?? false;
            if (isCompleted)
                return MissionRuntimeState.COMPLETED;

            var isWaiting = runtimeObj.Value<bool?>("isWaiting") ?? false;
            return isWaiting ? MissionRuntimeState.WAIT : MissionRuntimeState.ACTIVE;
        }

        static ACHIEVE_TYPE parseAchieveType(int? rawType, string achieveId)
        {
            if (rawType.HasValue && System.Enum.IsDefined(typeof(ACHIEVE_TYPE), rawType.Value))
            {
                var typed = (ACHIEVE_TYPE)rawType.Value;
                if (typed == ACHIEVE_TYPE.SOCIAL || typed == ACHIEVE_TYPE.PASS)
                    return typed;
            }

            if (!string.IsNullOrWhiteSpace(achieveId))
            {
                if (TB_ACHIEVE_PASS.GetByGroup(achieveId).Count > 0)
                    return ACHIEVE_TYPE.PASS;

                if (TB_ACHIEVE_SOCIAL.GetByGroup(achieveId).Count > 0)
                    return ACHIEVE_TYPE.SOCIAL;
            }

            return ACHIEVE_TYPE.SOCIAL;
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
