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

            var adsResetObj = new JObject();
            foreach (var kv in shop.adsCatalogResetStartedAtUtcMsByCatalog)
            {
                if (string.IsNullOrWhiteSpace(kv.Key))
                    continue;

                adsResetObj[kv.Key] = kv.Value > 0L ? kv.Value : 0L;
            }

            return new JObject
            {
                ["schemaVersion"] = shop.schemaVersion,
                ["lastResetUtcDayStartMs"] = shop.lastResetUtcDayStartMs,
                ["purchaseCounts"] = countsObj,
                ["adsCatalogResetStartedAtUtcMsByCatalog"] = adsResetObj,
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

            if (shopObj["adsCatalogResetStartedAtUtcMsByCatalog"] is JObject adsResetObj)
            {
                foreach (var prop in adsResetObj.Properties())
                {
                    var normalizedCatalogKey = prop.Name != null ? prop.Name.Trim() : string.Empty;
                    if (string.IsNullOrEmpty(normalizedCatalogKey))
                        continue;

                    var startedAtUtcMs = prop.Value.Value<long?>() ?? 0L;
                    shop.adsCatalogResetStartedAtUtcMsByCatalog[normalizedCatalogKey] =
                        startedAtUtcMs > 0L ? startedAtUtcMs : 0L;
                }
            }
            else if (shopObj["adsCatalogResetUtcDayStartMsByCatalog"] is JObject legacyAdsResetObj)
            {
                foreach (var prop in legacyAdsResetObj.Properties())
                {
                    var normalizedCatalogKey = prop.Name != null ? prop.Name.Trim() : string.Empty;
                    if (string.IsNullOrEmpty(normalizedCatalogKey))
                        continue;

                    var legacyResetUtcDayStartMs = prop.Value.Value<long?>() ?? 0L;
                    shop.adsCatalogResetStartedAtUtcMsByCatalog[normalizedCatalogKey] =
                        legacyResetUtcDayStartMs > 0L ? legacyResetUtcDayStartMs : 0L;
                }
            }

            if (shop.schemaVersion < 2)
                shop.schemaVersion = 2;
            if (shop.schemaVersion < 3)
                shop.schemaVersion = 3;
            if (shop.schemaVersion < 4)
                shop.schemaVersion = 4;
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
