using System;
using Newtonsoft.Json.Linq;
using Devian.Domain.Game;

namespace Devian
{
    internal static class SaveDataJsonCodecTreasure
    {
        public static JObject Serialize(TreasureStorage storage)
        {
            storage ??= new TreasureStorage();

            var currentObj = new JObject
            {
                ["exp"] = storage.Current.Exp,
                ["level"] = storage.Current.Level,
            };

            var treasureObj = new JObject
            {
                ["schemaVersion"] = storage.SchemaVersion,
                ["current"] = currentObj,
            };

            var chestCountsObj = new JObject();
            foreach (var kv in storage.ChestCounts)
            {
                if (kv.Key == TREASURE_GRADE_TYPE.NONE)
                    continue;

                if (kv.Value <= 0)
                    continue;

                chestCountsObj[kv.Key.ToString()] = kv.Value;
            }

            treasureObj["chestCounts"] = chestCountsObj;
            return treasureObj;
        }

        public static void DeserializeInto(JObject treasureObj, TreasureStorage storage)
        {
            if (storage == null)
                return;

            storage.Clear();

            if (treasureObj == null)
                return;

            storage.SchemaVersion = treasureObj.Value<int?>("schemaVersion") ?? 1;

            if (treasureObj["current"] is JObject currentObj)
            {
                var exp = currentObj.Value<int?>("exp") ?? 0;
                storage.Current.Exp = exp < 0 ? 0 : exp;

                var level = currentObj.Value<int?>("level") ?? 1;
                storage.Current.Level = level < 1 ? 1 : level;
            }

            if (treasureObj["chestCounts"] is JObject chestCountsObj)
            {
                foreach (var prop in chestCountsObj.Properties())
                {
                    if (string.IsNullOrWhiteSpace(prop.Name))
                        continue;

                    if (!Enum.TryParse<TREASURE_GRADE_TYPE>(prop.Name, out var gradeType))
                        continue;

                    if (gradeType == TREASURE_GRADE_TYPE.NONE)
                        continue;

                    var count = prop.Value.Value<int?>() ?? 0;
                    if (count <= 0)
                        continue;

                    storage.ChestCounts[gradeType] = count;
                }
            }
        }
    }
}
