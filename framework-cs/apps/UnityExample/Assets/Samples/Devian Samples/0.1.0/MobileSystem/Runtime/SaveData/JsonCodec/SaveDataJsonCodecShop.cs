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
                deserializeGroupedCatalogs(catalogsObj, shop);
            else
                deserializeLegacyFlat(shopObj, shop);

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
            if (shop.schemaVersion < 10)
                shop.schemaVersion = 10;
            if (shop.schemaVersion < 11)
                shop.schemaVersion = 11;
            if (shop.schemaVersion < 12)
                shop.schemaVersion = 12;
        }

        static JObject serializeCatalogs(ShopStorage shop)
        {
            var catalogsObj = new JObject();
            if (shop == null)
                return catalogsObj;

            addCatalog(catalogsObj, shop.daily);
            addCatalog(catalogsObj, shop.chest);
            addCatalog(catalogsObj, shop.purchase);
            addCatalog(catalogsObj, shop.gold);
            addCatalog(catalogsObj, shop.eventCatalog);
            return catalogsObj;
        }

        static void addCatalog(JObject catalogsObj, ShopCatalogStorageDataBase catalogData)
        {
            var stateObj = serializeCatalogData(catalogData);
            if (stateObj == null)
                return;

            catalogsObj[catalogData.CatalogType.ToString()] = stateObj;
        }

        static JObject serializeCatalogData(ShopCatalogStorageDataBase catalogData)
        {
            switch (catalogData)
            {
                case ShopCatalogDailyStorageData daily:
                    return serializeDailyCatalog(daily);
                case ShopCatalogChestStorageData chest:
                    return serializeChestCatalog(chest);
                case ShopCatalogGoldStorageData gold:
                    return serializeGoldCatalog(gold);
                case ShopCatalogEventStorageData eventCatalog:
                    return serializeEventCatalog(eventCatalog);
                case ShopCatalogPurchaseStorageData:
                default:
                    return null;
            }
        }

        static JObject serializeDailyCatalog(ShopCatalogDailyStorageData daily)
        {
            daily ??= new ShopCatalogDailyStorageData();
            var stateObj = new JObject
            {
                ["adsRefreshUtcMs"] = daily.adsRefreshUtcMs > 0L ? daily.adsRefreshUtcMs : 0L,
                ["autoRefreshUtcMs"] = daily.autoRefreshUtcMs > 0L ? daily.autoRefreshUtcMs : 0L,
                ["manualRefreshUtcMs"] = daily.manualRefreshUtcMs > 0L ? daily.manualRefreshUtcMs : 0L,
                ["manualRefreshRemainCount"] = daily.manualRefreshRemainCount > 0 ? daily.manualRefreshRemainCount : 0,
                ["productRemainCounts"] = serializeRemainCounts(daily.productRemainCounts),
                ["dailyCatalogProducts"] = serializeDailyProducts(daily.dailyCatalogProducts),
            };

            return hasMeaningfulState(stateObj) ? stateObj : null;
        }

        static JObject serializeChestCatalog(ShopCatalogChestStorageData chest)
        {
            chest ??= new ShopCatalogChestStorageData();
            var stateObj = new JObject
            {
                ["adsRefreshUtcMs"] = chest.adsRefreshUtcMs > 0L ? chest.adsRefreshUtcMs : 0L,
                ["level"] = chest.level > 0 ? chest.level : 1,
                ["currentExp"] = chest.currentExp > 0 ? chest.currentExp : 0,
                ["productRemainCounts"] = serializeRemainCounts(chest.productRemainCounts),
            };

            return hasMeaningfulState(stateObj) ? stateObj : null;
        }

        static JObject serializeGoldCatalog(ShopCatalogGoldStorageData gold)
        {
            gold ??= new ShopCatalogGoldStorageData();
            var stateObj = new JObject
            {
                ["adsRefreshUtcMs"] = gold.adsRefreshUtcMs > 0L ? gold.adsRefreshUtcMs : 0L,
                ["productRemainCounts"] = serializeRemainCounts(gold.productRemainCounts),
            };

            return hasMeaningfulState(stateObj) ? stateObj : null;
        }

        static JObject serializeEventCatalog(ShopCatalogEventStorageData eventCatalog)
        {
            eventCatalog ??= new ShopCatalogEventStorageData();
            var stateObj = new JObject
            {
                ["autoRefreshUtcMs"] = eventCatalog.autoRefreshUtcMs > 0L ? eventCatalog.autoRefreshUtcMs : 0L,
            };

            return hasMeaningfulState(stateObj) ? stateObj : null;
        }

        static bool hasMeaningfulState(JObject stateObj)
        {
            if (stateObj == null || stateObj.Count <= 0)
                return false;

            foreach (var property in stateObj.Properties())
            {
                switch (property.Value)
                {
                    case JValue value:
                        if (value.Type == JTokenType.Integer && (value.Value<long?>() ?? 0L) > 0L)
                            return true;
                        break;
                    case JObject obj when obj.Count > 0:
                        return true;
                    case JArray arr when arr.Count > 0:
                        return true;
                }
            }

            return false;
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

                switch (catalogType)
                {
                    case SHOP_CATALOG_TYPE.DAILY:
                        deserializeDailyCatalog(stateObj, shop);
                        break;
                    case SHOP_CATALOG_TYPE.CHEST:
                        deserializeChestCatalog(stateObj, shop);
                        break;
                    case SHOP_CATALOG_TYPE.GOLD:
                        deserializeGoldCatalog(stateObj, shop);
                        break;
                    case SHOP_CATALOG_TYPE.EVENT:
                        deserializeEventCatalog(stateObj, shop);
                        break;
                    case SHOP_CATALOG_TYPE.PURCHASE:
                    default:
                        break;
                }
            }
        }

        static void deserializeDailyCatalog(JObject stateObj, ShopStorage shop)
        {
            shop.SetAdsRefreshUtcMs(SHOP_CATALOG_TYPE.DAILY, stateObj.Value<long?>("adsRefreshUtcMs") ?? 0L);
            shop.SetAutoRefreshUtcMs(SHOP_CATALOG_TYPE.DAILY, stateObj.Value<long?>("autoRefreshUtcMs") ?? 0L);
            shop.SetManualRefreshUtcMs(SHOP_CATALOG_TYPE.DAILY, stateObj.Value<long?>("manualRefreshUtcMs") ?? 0L);
            var manualRefreshRemainCount =
                stateObj.Value<int?>("manualRefreshRemainCount")
                ?? stateObj.Value<int?>("manualRefreshCount")
                ?? 0;
            shop.SetManualRefreshRemainCount(SHOP_CATALOG_TYPE.DAILY, manualRefreshRemainCount);
            deserializeRemainCounts(stateObj["productRemainCounts"] as JObject, SHOP_CATALOG_TYPE.DAILY, shop);
            if (stateObj["dailyCatalogProducts"] is JArray dailyProductsArr)
                shop.SetDailyCatalogProducts(parseDailyProductStates(dailyProductsArr));
        }

        static void deserializeChestCatalog(JObject stateObj, ShopStorage shop)
        {
            shop.SetAdsRefreshUtcMs(SHOP_CATALOG_TYPE.CHEST, stateObj.Value<long?>("adsRefreshUtcMs") ?? 0L);
            shop.SetChestLevel(stateObj.Value<int?>("level") ?? 1);
            shop.SetChestCurrentExp(stateObj.Value<int?>("currentExp") ?? 0);
            deserializeRemainCounts(stateObj["productRemainCounts"] as JObject, SHOP_CATALOG_TYPE.CHEST, shop);
        }

        static void deserializeGoldCatalog(JObject stateObj, ShopStorage shop)
        {
            shop.SetAdsRefreshUtcMs(SHOP_CATALOG_TYPE.GOLD, stateObj.Value<long?>("adsRefreshUtcMs") ?? 0L);
            deserializeRemainCounts(stateObj["productRemainCounts"] as JObject, SHOP_CATALOG_TYPE.GOLD, shop);
        }

        static void deserializeEventCatalog(JObject stateObj, ShopStorage shop)
        {
            shop.SetAutoRefreshUtcMs(SHOP_CATALOG_TYPE.EVENT, stateObj.Value<long?>("autoRefreshUtcMs") ?? 0L);
        }

        static void deserializeRemainCounts(JObject remainObj, SHOP_CATALOG_TYPE catalogType, ShopStorage shop)
        {
            if (remainObj == null)
                return;

            foreach (var remainProp in remainObj.Properties())
            {
                var normalizedShopId = remainProp.Name != null ? remainProp.Name.Trim() : string.Empty;
                if (string.IsNullOrEmpty(normalizedShopId))
                    continue;

                var remainCount = remainProp.Value.Value<int?>() ?? -1;
                shop.SetProductRemainCount(catalogType, normalizedShopId, remainCount);
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
                shop.SetDailyCatalogProducts(parseDailyProductStates(dailyProductsArr));
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

            if (TB_SHOP_EVENT.Get(normalizedShopId) != null)
                return SHOP_CATALOG_TYPE.EVENT;

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
            if (shop == null)
                return;

            migrateLegacyAutoRefreshStartedAtToNextRefreshTime(shop, SHOP_CATALOG_TYPE.DAILY);
            migrateLegacyAutoRefreshStartedAtToNextRefreshTime(shop, SHOP_CATALOG_TYPE.EVENT);
        }

        static void migrateLegacyAutoRefreshStartedAtToNextRefreshTime(ShopStorage shop, SHOP_CATALOG_TYPE catalogType)
        {
            var startedAtUtcMs = shop.GetAutoRefreshUtcMs(catalogType);
            if (startedAtUtcMs <= 0L)
                return;

            if (!tryGetLegacyCatalogAutoRefreshIntervalMs(catalogType, out var intervalMs) || intervalMs <= 0L)
                return;

            shop.SetAutoRefreshUtcMs(catalogType, safeAddUtcMs(startedAtUtcMs, intervalMs));
        }

        static bool tryGetLegacyCatalogAutoRefreshIntervalMs(SHOP_CATALOG_TYPE catalogType, out long intervalMs)
        {
            intervalMs = 0L;
            if (catalogType == SHOP_CATALOG_TYPE.NONE)
                return false;

            var row = TB_SHOP_CATALOG.Get(catalogType);
            if (row == null || row.AutoRefreshDays <= 0)
                return false;

            intervalMs = row.AutoRefreshDays * 24L * 60L * 60L * 1000L;
            return intervalMs > 0L;
        }

        static long safeAddUtcMs(long left, long right)
        {
            if (left <= 0L || right <= 0L)
                return 0L;

            if (long.MaxValue - left < right)
                return long.MaxValue;

            return left + right;
        }
    }
}
