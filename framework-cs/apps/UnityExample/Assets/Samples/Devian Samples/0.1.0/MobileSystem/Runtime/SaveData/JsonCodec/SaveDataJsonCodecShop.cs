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

            var adsResetObj = new JObject();
            foreach (var kv in shop.adsCatalogResetStartedAtUtcMsByCatalog)
            {
                if (string.IsNullOrWhiteSpace(kv.Key))
                    continue;

                adsResetObj[kv.Key] = kv.Value > 0L ? kv.Value : 0L;
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
                ["adsCatalogResetStartedAtUtcMsByCatalog"] = adsResetObj,
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
    }
}
