using System;
using System.Collections.Generic;
using Devian.Domain.Game;
using Newtonsoft.Json.Linq;

namespace Devian
{
    internal static class SaveDataJsonCodecShop
    {
        public static JObject Serialize(ShopStorage shop)
        {
            shop ??= new ShopStorage();

            return new JObject
            {
                ["schemaVersion"] = shop.schemaVersion,
                ["catalogs"] = serializeCatalogs(shop),
            };
        }

        public static void DeserializeInto(JObject shopObj, ShopStorage shop)
        {
            if (shop == null)
                return;

            shop.Clear();
            shop.schemaVersion = shopObj.Value<int?>("schemaVersion") ?? 1;

            if (shopObj["catalogs"] is JObject catalogsObj)
            {
                deserializeGroupedCatalogs(catalogsObj, shop);
            }
            else
            {
                deserializeLegacyFlat(shopObj, shop);
            }

            if (shop.schemaVersion < 8)
                migrateLegacyAutoRefreshStartedAtToNextRefreshTime(shop);

            if (shop.schemaVersion < 2)
                shop.schemaVersion = 2;
            if (shop.schemaVersion < 3)
                shop.schemaVersion = 3;
            if (shop.schemaVersion < 4)
                shop.schemaVersion = 4;
            if (shop.schemaVersion < 5)
                shop.schemaVersion = 5;
            if (shop.schemaVersion < 6)
                shop.schemaVersion = 6;
            if (shop.schemaVersion < 7)
                shop.schemaVersion = 7;
            if (shop.schemaVersion < 8)
                shop.schemaVersion = 8;
            if (shop.schemaVersion < 9)
                shop.schemaVersion = 9;
        }

        static JObject serializeCatalogs(ShopStorage shop)
        {
            var catalogsObj = new JObject();
            if (shop?.catalogs == null || shop.catalogs.Count <= 0)
                return catalogsObj;

            foreach (var kv in shop.catalogs)
            {
                var catalogType = parseCatalogType(kv.Key);
                if (catalogType == SHOP_CATALOG_TYPE.NONE)
                    continue;

                var state = kv.Value ?? new ShopCatalogStorageState();
                var stateObj = new JObject
                {
                    ["adsRefreshUtcMs"] = state.adsRefreshUtcMs > 0L ? state.adsRefreshUtcMs : 0L,
                    ["productRemainCounts"] = serializeRemainCounts(state.productRemainCounts),
                };

                if (catalogType == SHOP_CATALOG_TYPE.DAILY)
                {
                    stateObj["autoRefreshUtcMs"] = state.autoRefreshUtcMs > 0L ? state.autoRefreshUtcMs : 0L;
                    stateObj["dailyCatalogProducts"] = serializeDailyProducts(state.dailyCatalogProducts);
                }

                catalogsObj[catalogType.ToString()] = stateObj;
            }

            return catalogsObj;
        }

        static JObject serializeRemainCounts(Dictionary<string, int> remainCounts)
        {
            var remainObj = new JObject();
            if (remainCounts == null || remainCounts.Count <= 0)
                return remainObj;

            foreach (var kv in remainCounts)
            {
                if (string.IsNullOrWhiteSpace(kv.Key))
                    continue;

                if (kv.Value < 0)
                    continue;

                remainObj[kv.Key.Trim()] = kv.Value;
            }

            return remainObj;
        }

        static JArray serializeDailyProducts(IReadOnlyList<ShopDailyProductState> dailyProducts)
        {
            var dailyProductsArr = new JArray();
            if (dailyProducts == null || dailyProducts.Count <= 0)
                return dailyProductsArr;

            for (var i = 0; i < dailyProducts.Count; i++)
            {
                var state = dailyProducts[i];
                if (state == null || string.IsNullOrWhiteSpace(state.shopId))
                    continue;

                dailyProductsArr.Add(new JObject
                {
                    ["shopId"] = state.shopId.Trim(),
                    ["discountType"] = (int)state.discountType,
                    ["remainCount"] = state.remainCount,
                });
            }

            return dailyProductsArr;
        }

        static void deserializeGroupedCatalogs(JObject catalogsObj, ShopStorage shop)
        {
            foreach (var catalogProp in catalogsObj.Properties())
            {
                var catalogType = parseCatalogType(catalogProp.Name);
                if (catalogType == SHOP_CATALOG_TYPE.NONE)
                    continue;

                if (catalogProp.Value is not JObject stateObj)
                    continue;

                var adsRefreshUtcMs = stateObj.Value<long?>("adsRefreshUtcMs") ?? 0L;
                shop.SetAdsRefreshUtcMs(catalogType, adsRefreshUtcMs);

                if (catalogType == SHOP_CATALOG_TYPE.DAILY)
                {
                    var autoRefreshUtcMs = stateObj.Value<long?>("autoRefreshUtcMs") ?? 0L;
                    shop.SetAutoRefreshUtcMs(catalogType, autoRefreshUtcMs);
                }

                if (stateObj["productRemainCounts"] is JObject remainObj)
                {
                    foreach (var remainProp in remainObj.Properties())
                    {
                        var normalizedShopId = remainProp.Name != null ? remainProp.Name.Trim() : string.Empty;
                        if (string.IsNullOrEmpty(normalizedShopId))
                            continue;

                        var remainCount = remainProp.Value.Value<int?>() ?? -1;
                        shop.SetProductRemainCount(catalogType, normalizedShopId, remainCount);
                    }
                }

                if (catalogType == SHOP_CATALOG_TYPE.DAILY
                    && stateObj["dailyCatalogProducts"] is JArray dailyProductsArr)
                {
                    shop.SetDailyCatalogProducts(parseDailyProductStates(dailyProductsArr));
                }
            }
        }

        static void deserializeLegacyFlat(JObject shopObj, ShopStorage shop)
        {
            if (shopObj["productRemainCounts"] is JObject remainObj)
            {
                foreach (var prop in remainObj.Properties())
                {
                    var normalizedShopId = prop.Name != null ? prop.Name.Trim() : string.Empty;
                    if (string.IsNullOrEmpty(normalizedShopId))
                        continue;

                    var catalogType = resolveCatalogTypeByShopId(normalizedShopId);
                    if (catalogType == SHOP_CATALOG_TYPE.NONE)
                        continue;

                    var remainCount = prop.Value.Value<int?>() ?? -1;
                    shop.SetProductRemainCount(catalogType, normalizedShopId, remainCount);
                }
            }

            if (shopObj["purchaseCounts"] is JObject legacyPurchaseCounts)
            {
                migrateLegacyPurchaseCounts(legacyPurchaseCounts, shop);
            }
            else if (shopObj["purchaseLimits"] is JObject legacyLimitsObj)
            {
                migrateLegacyPurchaseLimits(legacyLimitsObj, shop);
            }

            if (shopObj["autoRefreshUtcMsByCatalog"] is JObject autoRefreshObj)
            {
                foreach (var prop in autoRefreshObj.Properties())
                {
                    var catalogType = parseCatalogType(prop.Name);
                    if (catalogType != SHOP_CATALOG_TYPE.DAILY)
                        continue;

                    var autoRefreshUtcMs = prop.Value.Value<long?>() ?? 0L;
                    shop.SetAutoRefreshUtcMs(catalogType, autoRefreshUtcMs);
                }
            }
            else if (shopObj["adsCatalogResetStartedAtUtcMsByCatalog"] is JObject oldAutoRefreshObj)
            {
                foreach (var prop in oldAutoRefreshObj.Properties())
                {
                    var catalogType = parseCatalogType(prop.Name);
                    if (catalogType != SHOP_CATALOG_TYPE.DAILY)
                        continue;

                    var startedAtUtcMs = prop.Value.Value<long?>() ?? 0L;
                    shop.SetAutoRefreshUtcMs(catalogType, startedAtUtcMs);
                }
            }
            else if (shopObj["adsCatalogResetUtcDayStartMsByCatalog"] is JObject legacyAdsResetObj)
            {
                foreach (var prop in legacyAdsResetObj.Properties())
                {
                    var catalogType = parseCatalogType(prop.Name);
                    if (catalogType != SHOP_CATALOG_TYPE.DAILY)
                        continue;

                    var legacyResetUtcDayStartMs = prop.Value.Value<long?>() ?? 0L;
                    shop.SetAutoRefreshUtcMs(catalogType, legacyResetUtcDayStartMs);
                }
            }

            if (shopObj["adsRefreshUtcMsByCatalog"] is JObject adsRefreshObj)
            {
                foreach (var prop in adsRefreshObj.Properties())
                {
                    var catalogType = parseCatalogType(prop.Name);
                    if (catalogType == SHOP_CATALOG_TYPE.NONE)
                        continue;

                    var refreshUtcMs = prop.Value.Value<long?>() ?? 0L;
                    shop.SetAdsRefreshUtcMs(catalogType, refreshUtcMs);
                }
            }

            if (shopObj["dailyCatalogProducts"] is JArray dailyProductsArr)
            {
                shop.SetDailyCatalogProducts(parseDailyProductStates(dailyProductsArr));
            }
        }

        static ShopDailyProductState[] parseDailyProductStates(JArray dailyProductsArr)
        {
            if (dailyProductsArr == null || dailyProductsArr.Count <= 0)
                return Array.Empty<ShopDailyProductState>();

            var states = new ShopDailyProductState[dailyProductsArr.Count];
            var stateCount = 0;
            for (var i = 0; i < dailyProductsArr.Count; i++)
            {
                if (dailyProductsArr[i] is not JObject stateObj)
                    continue;

                var shopId = stateObj.Value<string>("shopId") ?? string.Empty;
                if (string.IsNullOrWhiteSpace(shopId))
                    continue;

                var discountTypeValue = stateObj.Value<int?>("discountType") ?? 0;
                var discountType = toDiscountType(discountTypeValue);
                var remainCount = stateObj.Value<int?>("remainCount") ?? -1;

                states[stateCount++] = new ShopDailyProductState
                {
                    shopId = shopId,
                    discountType = discountType,
                    remainCount = remainCount,
                };
            }

            if (stateCount <= 0)
                return Array.Empty<ShopDailyProductState>();

            if (stateCount == states.Length)
                return states;

            var compact = new ShopDailyProductState[stateCount];
            for (var i = 0; i < stateCount; i++)
                compact[i] = states[i];
            return compact;
        }

        static void migrateLegacyPurchaseCounts(JObject legacyPurchaseCountsObj, ShopStorage shop)
        {
            foreach (var prop in legacyPurchaseCountsObj.Properties())
            {
                var normalizedShopId = prop.Name != null ? prop.Name.Trim() : string.Empty;
                if (string.IsNullOrEmpty(normalizedShopId))
                    continue;

                var purchaseCount = prop.Value.Value<int?>() ?? 0;
                shop.SetLegacyPurchaseCount(normalizedShopId, purchaseCount);
            }
        }

        static void migrateLegacyPurchaseLimits(JObject legacyLimitsObj, ShopStorage shop)
        {
            foreach (var prop in legacyLimitsObj.Properties())
            {
                if (prop.Value is not JObject stateObj)
                    continue;

                var normalizedShopId = prop.Name != null ? prop.Name.Trim() : string.Empty;
                if (string.IsNullOrEmpty(normalizedShopId))
                    continue;

                var purchaseCount = stateObj.Value<int?>("purchaseCount") ?? 0;
                shop.SetLegacyPurchaseCount(normalizedShopId, purchaseCount);
            }
        }

        static SHOP_DISCOUNT_TYPE toDiscountType(int value)
        {
            switch (value)
            {
                case (int)SHOP_DISCOUNT_TYPE.PER10:
                    return SHOP_DISCOUNT_TYPE.PER10;
                case (int)SHOP_DISCOUNT_TYPE.PER20:
                    return SHOP_DISCOUNT_TYPE.PER20;
                case (int)SHOP_DISCOUNT_TYPE.PER30:
                    return SHOP_DISCOUNT_TYPE.PER30;
                case (int)SHOP_DISCOUNT_TYPE.PER50:
                    return SHOP_DISCOUNT_TYPE.PER50;
                default:
                    return SHOP_DISCOUNT_TYPE.NONE;
            }
        }

        static SHOP_CATALOG_TYPE parseCatalogType(string catalogKey)
        {
            if (string.IsNullOrWhiteSpace(catalogKey))
                return SHOP_CATALOG_TYPE.NONE;

            if (!Enum.TryParse(catalogKey.Trim(), true, out SHOP_CATALOG_TYPE catalogType))
                return SHOP_CATALOG_TYPE.NONE;

            return catalogType == SHOP_CATALOG_TYPE.NONE ? SHOP_CATALOG_TYPE.NONE : catalogType;
        }

        static SHOP_CATALOG_TYPE resolveCatalogTypeByShopId(string shopId)
        {
            if (string.IsNullOrWhiteSpace(shopId))
                return SHOP_CATALOG_TYPE.NONE;

            var normalizedShopId = shopId.Trim();
            if (TB_SHOP_DAILY.Get(normalizedShopId) != null)
                return SHOP_CATALOG_TYPE.DAILY;

            if (TB_SHOP_CHEST.Get(normalizedShopId) != null)
                return SHOP_CATALOG_TYPE.CHEST;

            if (TB_SHOP_PURCHASE.Get(normalizedShopId) != null)
                return SHOP_CATALOG_TYPE.PURCHASE;

            if (TB_SHOP_GOLD.Get(normalizedShopId) != null)
                return SHOP_CATALOG_TYPE.GOLD;

            return SHOP_CATALOG_TYPE.NONE;
        }

        static void migrateLegacyAutoRefreshStartedAtToNextRefreshTime(ShopStorage shop)
        {
            if (shop?.catalogs == null || shop.catalogs.Count <= 0)
                return;

            var keys = new List<string>(shop.catalogs.Keys);
            for (var i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                if (!shop.catalogs.TryGetValue(key, out var state) || state == null)
                    continue;

                var startedAtUtcMs = state.autoRefreshUtcMs;
                if (startedAtUtcMs <= 0L)
                    continue;

                if (!tryGetLegacyCatalogAutoRefreshIntervalMs(key, out var intervalMs) || intervalMs <= 0L)
                    continue;

                state.autoRefreshUtcMs = safeAddUtcMs(startedAtUtcMs, intervalMs);
            }
        }

        static bool tryGetLegacyCatalogAutoRefreshIntervalMs(string catalogKey, out long intervalMs)
        {
            intervalMs = 0L;
            var catalogType = parseCatalogType(catalogKey);
            if (catalogType == SHOP_CATALOG_TYPE.NONE)
                return false;

            switch (catalogType)
            {
                case SHOP_CATALOG_TYPE.DAILY:
                    intervalMs = 24L * 60L * 60L * 1000L;
                    return true;
                default:
                    return false;
            }
        }

        static long safeAddUtcMs(long utcMs, long addMs)
        {
            if (utcMs <= 0L || addMs <= 0L)
                return utcMs;

            if (long.MaxValue - utcMs < addMs)
                return long.MaxValue;

            return utcMs + addMs;
        }
    }
}
