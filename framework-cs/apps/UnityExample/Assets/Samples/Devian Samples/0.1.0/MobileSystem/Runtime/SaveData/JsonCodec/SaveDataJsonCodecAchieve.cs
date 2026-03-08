using Newtonsoft.Json.Linq;

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
                    ["achieveId"] = runtime.achieveId,
                    ["messageId"] = runtime.messageId,
                    ["achieveUid"] = runtime.achieveUid,
                    ["level"] = runtime.level,
                    ["progressValue"] = SerializeBigInt(runtime.progressValue),
                    ["isWaiting"] = runtime.isWaiting,
                    ["isCompleted"] = runtime.isCompleted,
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

                    var runtime = new AchieveRuntime
                    {
                        achieveId = runtimeObj.Value<string>("achieveId") ?? string.Empty,
                        messageId = runtimeObj.Value<string>("messageId") ?? string.Empty,
                        achieveUid = achieveUid,
                        level = runtimeObj.Value<int?>("level") ?? 1,
                        progressValue = DeserializeBigInt(runtimeObj["progressValue"]),
                        isWaiting = runtimeObj.Value<bool?>("isWaiting") ?? false,
                        isCompleted = runtimeObj.Value<bool?>("isCompleted") ?? false,
                    };

                    storage.runtimes[runtime.achieveUid] = runtime;
                }
            }

            if (storage.nextAchieveUid <= 0)
                storage.nextAchieveUid = 1;
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
