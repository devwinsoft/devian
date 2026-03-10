using Newtonsoft.Json.Linq;

namespace Devian
{
    internal static class SaveDataJsonCodecMessage
    {
        public static JObject Serialize(MetaMessageStorage storage)
        {
            storage ??= new MetaMessageStorage();

            var messageObj = new JObject
            {
                ["schemaVersion"] = storage.schemaVersion,
            };

            var statsObj = new JObject();
            foreach (var stat in storage.stats)
            {
                if (string.IsNullOrWhiteSpace(stat.Key))
                    continue;

                statsObj[stat.Key] = SerializeBigInt(stat.Value);
            }

            messageObj["stats"] = statsObj;
            return messageObj;
        }

        public static void DeserializeInto(JObject messageObj, MetaMessageStorage storage)
        {
            if (storage == null)
                return;

            storage.Clear();

            if (messageObj == null)
                return;

            storage.schemaVersion = messageObj.Value<int?>("schemaVersion") ?? 1;
            if (messageObj["stats"] is JObject statsObj)
            {
                foreach (var property in statsObj.Properties())
                {
                    if (string.IsNullOrWhiteSpace(property.Name))
                        continue;

                    storage.SetStat(property.Name, DeserializeBigInt(property.Value));
                }
            }
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
