using Newtonsoft.Json.Linq;

namespace Devian
{
    internal static class SaveDataJsonCodecShop
    {
        public static JObject Serialize(ShopStorage shop)
        {
            var limitsObj = new JObject();
            foreach (var kv in shop.purchaseLimits)
            {
                if (string.IsNullOrWhiteSpace(kv.Key) || kv.Value == null)
                    continue;

                limitsObj[kv.Key] = new JObject
                {
                    ["periodStartUtcMs"] = kv.Value.periodStartUtcMs,
                    ["purchaseCount"] = kv.Value.purchaseCount,
                };
            }

            return new JObject
            {
                ["schemaVersion"] = shop.schemaVersion,
                ["purchaseLimits"] = limitsObj,
            };
        }

        public static void DeserializeInto(JObject shopObj, ShopStorage shop)
        {
            shop.Clear();
            shop.schemaVersion = shopObj.Value<int?>("schemaVersion") ?? 1;

            if (shopObj["purchaseLimits"] is not JObject limitsObj)
                return;

            foreach (var prop in limitsObj.Properties())
            {
                if (prop.Value is not JObject stateObj)
                    continue;

                var state = shop.GetOrCreatePurchaseLimit(prop.Name);
                if (state == null)
                    continue;

                state.periodStartUtcMs = stateObj.Value<long?>("periodStartUtcMs") ?? 0L;
                state.purchaseCount = stateObj.Value<int?>("purchaseCount") ?? 0;
                if (state.purchaseCount < 0)
                    state.purchaseCount = 0;
            }
        }
    }
}
