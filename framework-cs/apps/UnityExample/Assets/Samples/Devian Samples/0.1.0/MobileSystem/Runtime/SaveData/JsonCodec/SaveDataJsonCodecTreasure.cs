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

            var progressObj = new JObject
            {
                ["currentExp"] = storage.Progress.CurrentExp,
                ["currentLevel"] = storage.Progress.CurrentLevel,
            };

            var treasureObj = new JObject
            {
                ["schemaVersion"] = storage.SchemaVersion,
                ["progress"] = progressObj,
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

            if (treasureObj["progress"] is JObject progressObj)
            {
                var currentExp = progressObj.Value<int?>("currentExp") ?? 0;
                storage.Progress.CurrentExp = currentExp < 0 ? 0 : currentExp;

                var currentLevel = progressObj.Value<int?>("currentLevel") ?? 1;
                storage.Progress.CurrentLevel = currentLevel < 1 ? 1 : currentLevel;
            }
            else
            {
                // backward compat: flat currentExp/currentLevel
                var currentExp = treasureObj.Value<int?>("currentExp") ?? 0;
                storage.Progress.CurrentExp = currentExp < 0 ? 0 : currentExp;

                var currentLevel = treasureObj.Value<int?>("currentLevel") ?? 1;
                storage.Progress.CurrentLevel = currentLevel < 1 ? 1 : currentLevel;
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
