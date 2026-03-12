using Newtonsoft.Json.Linq;

namespace Devian
{
    internal static class SaveDataJsonCodecShop
    {
        public static JObject Serialize(ShopStorage shop)
        {
            shop ??= new ShopStorage();

            var countsObj = new JObject();
            foreach (var kv in shop.purchaseCounts)
            {
                if (string.IsNullOrWhiteSpace(kv.Key))
                    continue;

                countsObj[kv.Key] = kv.Value < 0 ? 0 : kv.Value;
            }

            return new JObject
            {
                ["schemaVersion"] = shop.schemaVersion,
                ["lastResetUtcDayStartMs"] = shop.lastResetUtcDayStartMs,
                ["purchaseCounts"] = countsObj,
            };
        }

        public static void DeserializeInto(JObject shopObj, ShopStorage shop)
        {
            if (shop == null)
                return;

            shop.Clear();
            shop.schemaVersion = shopObj.Value<int?>("schemaVersion") ?? 1;
            shop.lastResetUtcDayStartMs = shopObj.Value<long?>("lastResetUtcDayStartMs") ?? 0L;

            if (shopObj["purchaseCounts"] is JObject countsObj)
            {
                foreach (var prop in countsObj.Properties())
                {
                    var normalizedShopId = prop.Name != null ? prop.Name.Trim() : string.Empty;
                    if (string.IsNullOrEmpty(normalizedShopId))
                        continue;

                    var purchaseCount = prop.Value.Value<int?>() ?? 0;
                    shop.SetPurchaseCount(normalizedShopId, purchaseCount);
                }
            }
            else if (shopObj["purchaseLimits"] is JObject legacyLimitsObj)
            {
                migrateLegacyPurchaseLimits(legacyLimitsObj, shop);
            }

            if (shop.schemaVersion < 2)
                shop.schemaVersion = 2;
        }

        static void migrateLegacyPurchaseLimits(JObject legacyLimitsObj, ShopStorage shop)
        {
            var migratedDayStartUtcMs = 0L;
            foreach (var prop in legacyLimitsObj.Properties())
            {
                if (prop.Value is not JObject stateObj)
                    continue;

                var normalizedShopId = prop.Name != null ? prop.Name.Trim() : string.Empty;
                if (string.IsNullOrEmpty(normalizedShopId))
                    continue;

                var purchaseCount = stateObj.Value<int?>("purchaseCount") ?? 0;
                shop.SetPurchaseCount(normalizedShopId, purchaseCount);

                var periodStartUtcMs = stateObj.Value<long?>("periodStartUtcMs") ?? 0L;
                if (periodStartUtcMs > migratedDayStartUtcMs)
                    migratedDayStartUtcMs = periodStartUtcMs;
            }

            if (migratedDayStartUtcMs > 0L)
                shop.lastResetUtcDayStartMs = migratedDayStartUtcMs;
        }
    }
}
