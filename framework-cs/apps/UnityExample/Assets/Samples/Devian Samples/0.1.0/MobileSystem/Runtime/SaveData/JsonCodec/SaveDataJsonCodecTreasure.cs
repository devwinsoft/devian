using System;
using Newtonsoft.Json.Linq;
using Devian.Domain.Game;

namespace Devian
{
    internal static class SaveDataJsonCodecTreasure
    {
        const int SchemaVersion = 1;

        public static JObject Serialize(InventoryStorage storage)
        {
            storage ??= new InventoryStorage();

            var currentObj = new JObject
            {
                ["exp"] = storage.TreasureCurrent.Exp,
                ["level"] = storage.TreasureCurrent.Level,
            };

            var treasureObj = new JObject
            {
                ["schemaVersion"] = SchemaVersion,
                ["current"] = currentObj,
            };

            var chestCountsObj = new JObject();
            foreach (var kv in storage.TreasureCounts)
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

        public static void DeserializeInto(JObject treasureObj, InventoryStorage storage)
        {
            if (storage == null)
                return;

            storage.TreasureCounts.Clear();
            storage.TreasureCurrent.Clear();

            if (treasureObj == null)
                return;

            if (treasureObj["current"] is JObject currentObj)
            {
                var exp = currentObj.Value<int?>("exp") ?? 0;
                storage.TreasureCurrent.Exp = exp < 0 ? 0 : exp;

                var level = currentObj.Value<int?>("level") ?? 1;
                storage.TreasureCurrent.Level = level < 1 ? 1 : level;
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

                    storage.TreasureCounts[gradeType] = count;
                }
            }
        }
    }
}
