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

            var remainObj = new JObject();
            foreach (var kv in shop.productRemainCounts)
            {
                if (string.IsNullOrWhiteSpace(kv.Key))
                    continue;

                if (kv.Value < 0)
                    continue;

                remainObj[kv.Key] = kv.Value;
            }

            var autoRefreshObj = new JObject();
            foreach (var kv in shop.autoRefreshUtcMsByCatalog)
            {
                if (string.IsNullOrWhiteSpace(kv.Key))
                    continue;

                autoRefreshObj[kv.Key] = kv.Value > 0L ? kv.Value : 0L;
            }

            var adsRefreshObj = new JObject();
            foreach (var kv in shop.adsRefreshUtcMsByCatalog)
            {
                if (string.IsNullOrWhiteSpace(kv.Key))
                    continue;

                adsRefreshObj[kv.Key] = kv.Value > 0L ? kv.Value : 0L;
            }

            var dailyProductsArr = new JArray();
            var dailyProducts = shop.GetDailyCatalogProducts();
            if (dailyProducts != null)
            {
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
            }

            return new JObject
            {
                ["schemaVersion"] = shop.schemaVersion,
                ["productRemainCounts"] = remainObj,
                ["autoRefreshUtcMsByCatalog"] = autoRefreshObj,
                ["adsRefreshUtcMsByCatalog"] = adsRefreshObj,
                ["dailyCatalogProducts"] = dailyProductsArr,
            };
        }

        public static void DeserializeInto(JObject shopObj, ShopStorage shop)
        {
            if (shop == null)
                return;

            shop.Clear();
            shop.schemaVersion = shopObj.Value<int?>("schemaVersion") ?? 1;

            if (shopObj["productRemainCounts"] is JObject remainObj)
            {
                foreach (var prop in remainObj.Properties())
                {
                    var normalizedShopId = prop.Name != null ? prop.Name.Trim() : string.Empty;
                    if (string.IsNullOrEmpty(normalizedShopId))
                        continue;

                    var remainCount = prop.Value.Value<int?>() ?? -1;
                    shop.SetProductRemainCount(normalizedShopId, remainCount);
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
                    var normalizedCatalogKey = prop.Name != null ? prop.Name.Trim() : string.Empty;
                    if (string.IsNullOrEmpty(normalizedCatalogKey))
                        continue;

                    var autoRefreshUtcMs = prop.Value.Value<long?>() ?? 0L;
                    shop.autoRefreshUtcMsByCatalog[normalizedCatalogKey] =
                        autoRefreshUtcMs > 0L ? autoRefreshUtcMs : 0L;
                }
            }
            else if (shopObj["adsCatalogResetStartedAtUtcMsByCatalog"] is JObject oldAutoRefreshObj)
            {
                foreach (var prop in oldAutoRefreshObj.Properties())
                {
                    var normalizedCatalogKey = prop.Name != null ? prop.Name.Trim() : string.Empty;
                    if (string.IsNullOrEmpty(normalizedCatalogKey))
                        continue;

                    var startedAtUtcMs = prop.Value.Value<long?>() ?? 0L;
                    shop.autoRefreshUtcMsByCatalog[normalizedCatalogKey] =
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
                    shop.autoRefreshUtcMsByCatalog[normalizedCatalogKey] =
                        legacyResetUtcDayStartMs > 0L ? legacyResetUtcDayStartMs : 0L;
                }
            }

            if (shopObj["adsRefreshUtcMsByCatalog"] is JObject adsRefreshObj)
            {
                foreach (var prop in adsRefreshObj.Properties())
                {
                    var normalizedCatalogKey = prop.Name != null ? prop.Name.Trim() : string.Empty;
                    if (string.IsNullOrEmpty(normalizedCatalogKey))
                        continue;

                    var refreshUtcMs = prop.Value.Value<long?>() ?? 0L;
                    shop.adsRefreshUtcMsByCatalog[normalizedCatalogKey] =
                        refreshUtcMs > 0L ? refreshUtcMs : 0L;
                }
            }

            if (shopObj["dailyCatalogProducts"] is JArray dailyProductsArr)
            {
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

                if (stateCount > 0)
                {
                    var compact = new ShopDailyProductState[stateCount];
                    for (var i = 0; i < stateCount; i++)
                        compact[i] = states[i];

                    shop.SetDailyCatalogProducts(compact);
                }
                else
                {
                    shop.ClearDailyCatalogProducts();
                }
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

        static void migrateLegacyAutoRefreshStartedAtToNextRefreshTime(ShopStorage shop)
        {
            if (shop?.autoRefreshUtcMsByCatalog == null || shop.autoRefreshUtcMsByCatalog.Count <= 0)
                return;

            var keys = new List<string>(shop.autoRefreshUtcMsByCatalog.Keys);
            for (var i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                if (!shop.autoRefreshUtcMsByCatalog.TryGetValue(key, out var startedAtUtcMs) || startedAtUtcMs <= 0L)
                    continue;

                if (!tryGetLegacyCatalogAutoRefreshIntervalMs(key, out var intervalMs) || intervalMs <= 0L)
                    continue;

                shop.autoRefreshUtcMsByCatalog[key] = safeAddUtcMs(startedAtUtcMs, intervalMs);
            }
        }

        static bool tryGetLegacyCatalogAutoRefreshIntervalMs(string catalogKey, out long intervalMs)
        {
            intervalMs = 0L;
            if (string.IsNullOrWhiteSpace(catalogKey))
                return false;

            if (!Enum.TryParse(catalogKey.Trim(), true, out SHOP_CATALOG_TYPE catalogType))
                return false;

            switch (catalogType)
            {
                case SHOP_CATALOG_TYPE.DAILY:
                case SHOP_CATALOG_TYPE.CHEST:
                case SHOP_CATALOG_TYPE.GOLD:
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
