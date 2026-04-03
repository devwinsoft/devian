using System;
using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    [Serializable]
    public sealed class ShopDailyProductState
    {
        public string shopItemId = string.Empty;
        public SHOP_DISCOUNT_TYPE discountType = SHOP_DISCOUNT_TYPE.NONE;
        public int remainCount = -1;
    }

    [Serializable]
    public abstract class ShopCatalogStorageDataBase
    {
        public abstract SHOP_CATALOG_TYPE CatalogType { get; }
    }

    [Serializable]
    public abstract class ShopCatalogProductRemainStorageDataBase : ShopCatalogStorageDataBase
    {
        public Dictionary<string, int> productRemainCounts = new();
    }

    [Serializable]
    public sealed class ShopCatalogDailyStorageData : ShopCatalogProductRemainStorageDataBase
    {
        public override SHOP_CATALOG_TYPE CatalogType => SHOP_CATALOG_TYPE.DAILY;

        public long autoRefreshUtcMs;
        public long adsRefreshUtcMs;
        public long manualRefreshUtcMs;
        public int manualRefreshRemainCount = ShopCatalogDaily.MaxManualRefreshCountPerDay;
        public List<ShopDailyProductState> dailyCatalogProducts = new();
    }

    [Serializable]
    public sealed class ShopCatalogChestStorageData : ShopCatalogProductRemainStorageDataBase
    {
        public override SHOP_CATALOG_TYPE CatalogType => SHOP_CATALOG_TYPE.CHEST;

        public long adsRefreshUtcMs;
        public int level = 1;
        public int currentExp;
    }

    [Serializable]
    public sealed class ShopCatalogPurchaseStorageData : ShopCatalogStorageDataBase
    {
        public override SHOP_CATALOG_TYPE CatalogType => SHOP_CATALOG_TYPE.PURCHASE;
    }

    [Serializable]
    public sealed class ShopCatalogGoldStorageData : ShopCatalogProductRemainStorageDataBase
    {
        public override SHOP_CATALOG_TYPE CatalogType => SHOP_CATALOG_TYPE.GOLD;

        public long adsRefreshUtcMs;
    }

    [Serializable]
    public sealed class ShopCatalogEventStorageData : ShopCatalogStorageDataBase
    {
        public override SHOP_CATALOG_TYPE CatalogType => SHOP_CATALOG_TYPE.EVENT;

        public long autoRefreshUtcMs;
    }

    [Serializable]
    public sealed class ShopStorage
    {
        public int schemaVersion = 13;
        public ShopCatalogDailyStorageData daily = new();
        public ShopCatalogChestStorageData chest = new();
        public ShopCatalogPurchaseStorageData purchase = new();
        public ShopCatalogGoldStorageData gold = new();
        public ShopCatalogEventStorageData eventCatalog = new();

        [NonSerialized]
        readonly Dictionary<string, int> _legacyPurchaseCounts = new();

        public ShopCatalogStorageDataBase GetCatalogData(SHOP_CATALOG_TYPE catalogType)
        {
            ensureCatalogData();
            return catalogType switch
            {
                SHOP_CATALOG_TYPE.DAILY => daily,
                SHOP_CATALOG_TYPE.CHEST => chest,
                SHOP_CATALOG_TYPE.PURCHASE => purchase,
                SHOP_CATALOG_TYPE.GOLD => gold,
                SHOP_CATALOG_TYPE.EVENT => eventCatalog,
                _ => null,
            };
        }

        public T GetCatalogData<T>(SHOP_CATALOG_TYPE catalogType) where T : ShopCatalogStorageDataBase
        {
            return GetCatalogData(catalogType) as T;
        }

        public bool TryGetProductRemainCount(
            SHOP_CATALOG_TYPE catalogType,
            string shopItemId,
            out int remainCount)
        {
            remainCount = -1;
            var state = getProductRemainData(catalogType);
            if (state == null)
                return false;

            var key = normalizeShopItemId(shopItemId);
            if (string.IsNullOrEmpty(key))
                return false;

            return state.productRemainCounts.TryGetValue(key, out remainCount);
        }

        public void SetProductRemainCount(
            SHOP_CATALOG_TYPE catalogType,
            string shopItemId,
            int remainCount)
        {
            var key = normalizeShopItemId(shopItemId);
            if (string.IsNullOrEmpty(key))
                return;

            var state = getProductRemainData(catalogType);
            if (state == null)
                return;

            var normalizedRemainCount = normalizeLimitedRemainCount(remainCount);
            if (normalizedRemainCount < 0)
            {
                state.productRemainCounts.Remove(key);
                return;
            }

            state.productRemainCounts[key] = normalizedRemainCount;
        }

        public void RemoveProductRemainCount(SHOP_CATALOG_TYPE catalogType, string shopItemId)
        {
            var state = getProductRemainData(catalogType);
            if (state == null)
                return;

            var key = normalizeShopItemId(shopItemId);
            if (string.IsNullOrEmpty(key))
                return;

            state.productRemainCounts.Remove(key);
        }

        public void ClearProductRemainCounts(
            SHOP_CATALOG_TYPE catalogType,
            IReadOnlyList<string> shopItemIds)
        {
            var state = getProductRemainData(catalogType);
            if (state == null || shopItemIds == null || shopItemIds.Count <= 0)
                return;

            for (var i = 0; i < shopItemIds.Count; i++)
            {
                var key = normalizeShopItemId(shopItemIds[i]);
                if (string.IsNullOrEmpty(key))
                    continue;

                state.productRemainCounts.Remove(key);
            }
        }

        public bool TryGetProductRemainCount(string shopItemId, out int remainCount)
        {
            remainCount = -1;
            var key = normalizeShopItemId(shopItemId);
            if (string.IsNullOrEmpty(key))
                return false;

            ensureCatalogData();
            return tryGetProductRemainCount(daily, key, out remainCount)
                || tryGetProductRemainCount(chest, key, out remainCount)
                || tryGetProductRemainCount(gold, key, out remainCount);
        }

        public void SetProductRemainCount(string shopItemId, int remainCount)
        {
            var catalogType = resolveCatalogTypeFromTable(shopItemId);
            if (catalogType == SHOP_CATALOG_TYPE.NONE)
                return;

            SetProductRemainCount(catalogType, shopItemId, remainCount);
        }

        public void RemoveProductRemainCount(string shopItemId)
        {
            var key = normalizeShopItemId(shopItemId);
            if (string.IsNullOrEmpty(key))
                return;

            ensureCatalogData();
            daily.productRemainCounts.Remove(key);
            chest.productRemainCounts.Remove(key);
            gold.productRemainCounts.Remove(key);
        }

        public void ClearProductRemainCounts(IReadOnlyList<string> shopItemIds)
        {
            if (shopItemIds == null || shopItemIds.Count <= 0)
                return;

            ensureCatalogData();
            for (var i = 0; i < shopItemIds.Count; i++)
            {
                var key = normalizeShopItemId(shopItemIds[i]);
                if (string.IsNullOrEmpty(key))
                    continue;

                daily.productRemainCounts.Remove(key);
                chest.productRemainCounts.Remove(key);
                gold.productRemainCounts.Remove(key);
            }
        }

        internal void SetLegacyPurchaseCount(string shopItemId, int purchaseCount)
        {
            var key = normalizeShopItemId(shopItemId);
            if (string.IsNullOrEmpty(key))
                return;

            _legacyPurchaseCounts[key] = purchaseCount < 0 ? 0 : purchaseCount;
        }

        internal bool TryTakeLegacyPurchaseCount(string shopItemId, out int purchaseCount)
        {
            purchaseCount = 0;
            var key = normalizeShopItemId(shopItemId);
            if (string.IsNullOrEmpty(key))
                return false;

            if (!_legacyPurchaseCounts.TryGetValue(key, out purchaseCount))
                return false;

            _legacyPurchaseCounts.Remove(key);
            return true;
        }

        internal void ClearLegacyPurchaseCounts()
        {
            _legacyPurchaseCounts.Clear();
        }

        public long GetAutoRefreshUtcMs(SHOP_CATALOG_TYPE catalogType)
        {
            ensureCatalogData();
            return catalogType switch
            {
                SHOP_CATALOG_TYPE.DAILY => normalizeUtcMs(daily.autoRefreshUtcMs),
                SHOP_CATALOG_TYPE.EVENT => normalizeUtcMs(eventCatalog.autoRefreshUtcMs),
                _ => 0L,
            };
        }

        public void SetAutoRefreshUtcMs(SHOP_CATALOG_TYPE catalogType, long utcMs)
        {
            ensureCatalogData();
            var normalizedUtcMs = normalizeUtcMs(utcMs);
            switch (catalogType)
            {
                case SHOP_CATALOG_TYPE.DAILY:
                    daily.autoRefreshUtcMs = normalizedUtcMs;
                    break;
                case SHOP_CATALOG_TYPE.EVENT:
                    eventCatalog.autoRefreshUtcMs = normalizedUtcMs;
                    break;
            }
        }

        public void ClearAutoRefreshUtcMs(SHOP_CATALOG_TYPE catalogType)
        {
            SetAutoRefreshUtcMs(catalogType, 0L);
        }

        public long GetAdsRefreshUtcMs(SHOP_CATALOG_TYPE catalogType)
        {
            ensureCatalogData();
            return catalogType switch
            {
                SHOP_CATALOG_TYPE.DAILY => normalizeUtcMs(daily.adsRefreshUtcMs),
                SHOP_CATALOG_TYPE.CHEST => normalizeUtcMs(chest.adsRefreshUtcMs),
                SHOP_CATALOG_TYPE.GOLD => normalizeUtcMs(gold.adsRefreshUtcMs),
                _ => 0L,
            };
        }

        public void SetAdsRefreshUtcMs(SHOP_CATALOG_TYPE catalogType, long utcMs)
        {
            ensureCatalogData();
            var normalizedUtcMs = normalizeUtcMs(utcMs);
            switch (catalogType)
            {
                case SHOP_CATALOG_TYPE.DAILY:
                    daily.adsRefreshUtcMs = normalizedUtcMs;
                    break;
                case SHOP_CATALOG_TYPE.CHEST:
                    chest.adsRefreshUtcMs = normalizedUtcMs;
                    break;
                case SHOP_CATALOG_TYPE.GOLD:
                    gold.adsRefreshUtcMs = normalizedUtcMs;
                    break;
            }
        }

        public void ClearAdsRefreshUtcMs(SHOP_CATALOG_TYPE catalogType)
        {
            SetAdsRefreshUtcMs(catalogType, 0L);
        }

        public int GetChestLevel()
        {
            ensureCatalogData();
            return normalizeChestLevel(chest.level);
        }

        public void SetChestLevel(int level)
        {
            ensureCatalogData();
            chest.level = normalizeChestLevel(level);
        }

        public int GetChestCurrentExp()
        {
            ensureCatalogData();
            return normalizeChestCurrentExp(chest.currentExp);
        }

        public void SetChestCurrentExp(int currentExp)
        {
            ensureCatalogData();
            chest.currentExp = normalizeChestCurrentExp(currentExp);
        }

        public long GetManualRefreshUtcMs(SHOP_CATALOG_TYPE catalogType)
        {
            if (catalogType != SHOP_CATALOG_TYPE.DAILY)
                return 0L;

            ensureCatalogData();
            return normalizeUtcMs(daily.manualRefreshUtcMs);
        }

        public void SetManualRefreshUtcMs(SHOP_CATALOG_TYPE catalogType, long utcMs)
        {
            if (catalogType != SHOP_CATALOG_TYPE.DAILY)
                return;

            ensureCatalogData();
            daily.manualRefreshUtcMs = normalizeUtcMs(utcMs);
        }

        public void ClearManualRefreshUtcMs(SHOP_CATALOG_TYPE catalogType)
        {
            if (catalogType != SHOP_CATALOG_TYPE.DAILY)
                return;

            ensureCatalogData();
            daily.manualRefreshUtcMs = 0L;
        }

        public int GetManualRefreshRemainCount(SHOP_CATALOG_TYPE catalogType)
        {
            if (catalogType != SHOP_CATALOG_TYPE.DAILY)
                return 0;

            ensureCatalogData();
            return normalizeManualRefreshRemainCount(daily.manualRefreshRemainCount);
        }

        public void SetManualRefreshRemainCount(SHOP_CATALOG_TYPE catalogType, int manualRefreshRemainCount)
        {
            if (catalogType != SHOP_CATALOG_TYPE.DAILY)
                return;

            ensureCatalogData();
            daily.manualRefreshRemainCount = normalizeManualRefreshRemainCount(manualRefreshRemainCount);
        }

        public void ClearManualRefreshRemainCount(SHOP_CATALOG_TYPE catalogType)
        {
            if (catalogType != SHOP_CATALOG_TYPE.DAILY)
                return;

            ensureCatalogData();
            daily.manualRefreshRemainCount = ShopCatalogDaily.MaxManualRefreshCountPerDay;
        }

        public IReadOnlyList<ShopDailyProductState> GetDailyCatalogProducts()
        {
            ensureCatalogData();
            return daily.dailyCatalogProducts != null
                ? daily.dailyCatalogProducts
                : Array.Empty<ShopDailyProductState>();
        }

        public bool TryGetDailyCatalogProduct(string shopItemId, out ShopDailyProductState state)
        {
            state = null;
            ensureCatalogData();

            var key = normalizeShopItemId(shopItemId);
            if (string.IsNullOrEmpty(key))
                return false;

            var dailyProducts = daily.dailyCatalogProducts;
            for (var i = 0; i < dailyProducts.Count; i++)
            {
                var item = dailyProducts[i];
                if (item == null)
                    continue;

                if (!string.Equals(normalizeShopItemId(item.shopItemId), key, StringComparison.Ordinal))
                    continue;

                state = cloneDailyState(item);
                return true;
            }

            return false;
        }

        public void SetDailyCatalogProducts(IReadOnlyList<ShopDailyProductState> states)
        {
            ensureCatalogData();
            daily.dailyCatalogProducts.Clear();
            if (states == null || states.Count <= 0)
                return;

            var seenShopItemIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (state == null)
                    continue;

                var key = normalizeShopItemId(state.shopItemId);
                if (string.IsNullOrEmpty(key))
                    continue;

                if (!seenShopItemIds.Add(key))
                    continue;

                daily.dailyCatalogProducts.Add(createDailyState(key, state.discountType, state.remainCount));
            }
        }

        public void UpsertDailyCatalogProduct(
            string shopItemId,
            SHOP_DISCOUNT_TYPE discountType,
            int remainCount)
        {
            ensureCatalogData();
            var key = normalizeShopItemId(shopItemId);
            if (string.IsNullOrEmpty(key))
                return;

            var normalizedState = createDailyState(key, discountType, remainCount);
            var dailyProducts = daily.dailyCatalogProducts;
            for (var i = 0; i < dailyProducts.Count; i++)
            {
                var item = dailyProducts[i];
                if (item == null)
                    continue;

                if (!string.Equals(normalizeShopItemId(item.shopItemId), key, StringComparison.Ordinal))
                    continue;

                dailyProducts[i] = normalizedState;
                return;
            }

            dailyProducts.Add(normalizedState);
        }

        public void RemoveDailyCatalogProduct(string shopItemId)
        {
            ensureCatalogData();
            var key = normalizeShopItemId(shopItemId);
            if (string.IsNullOrEmpty(key))
                return;

            var dailyProducts = daily.dailyCatalogProducts;
            for (var i = dailyProducts.Count - 1; i >= 0; i--)
            {
                var item = dailyProducts[i];
                if (item == null)
                {
                    dailyProducts.RemoveAt(i);
                    continue;
                }

                if (string.Equals(normalizeShopItemId(item.shopItemId), key, StringComparison.Ordinal))
                    dailyProducts.RemoveAt(i);
            }
        }

        public void ClearDailyCatalogProducts()
        {
            ensureCatalogData();
            daily.dailyCatalogProducts.Clear();
        }

        public void Clear()
        {
            schemaVersion = 13;
            daily = new ShopCatalogDailyStorageData();
            chest = new ShopCatalogChestStorageData();
            purchase = new ShopCatalogPurchaseStorageData();
            gold = new ShopCatalogGoldStorageData();
            eventCatalog = new ShopCatalogEventStorageData();
            _legacyPurchaseCounts.Clear();
        }

        void ensureCatalogData()
        {
            daily ??= new ShopCatalogDailyStorageData();
            chest ??= new ShopCatalogChestStorageData();
            purchase ??= new ShopCatalogPurchaseStorageData();
            gold ??= new ShopCatalogGoldStorageData();
            eventCatalog ??= new ShopCatalogEventStorageData();
            daily.productRemainCounts ??= new Dictionary<string, int>();
            chest.productRemainCounts ??= new Dictionary<string, int>();
            gold.productRemainCounts ??= new Dictionary<string, int>();
            daily.dailyCatalogProducts ??= new List<ShopDailyProductState>();
        }

        ShopCatalogProductRemainStorageDataBase getProductRemainData(SHOP_CATALOG_TYPE catalogType)
        {
            ensureCatalogData();
            return catalogType switch
            {
                SHOP_CATALOG_TYPE.DAILY => daily,
                SHOP_CATALOG_TYPE.CHEST => chest,
                SHOP_CATALOG_TYPE.GOLD => gold,
                _ => null,
            };
        }

        static bool tryGetProductRemainCount(
            ShopCatalogProductRemainStorageDataBase state,
            string normalizedShopItemId,
            out int remainCount)
        {
            remainCount = -1;
            return state != null
                && state.productRemainCounts != null
                && state.productRemainCounts.TryGetValue(normalizedShopItemId, out remainCount);
        }

        static ShopDailyProductState createDailyState(
            string shopItemId,
            SHOP_DISCOUNT_TYPE discountType,
            int remainCount)
        {
            return new ShopDailyProductState
            {
                shopItemId = normalizeShopItemId(shopItemId),
                discountType = normalizeDiscountType(discountType),
                remainCount = normalizeRemainCount(remainCount),
            };
        }

        static ShopDailyProductState cloneDailyState(ShopDailyProductState state)
        {
            if (state == null)
                return null;

            return createDailyState(state.shopItemId, state.discountType, state.remainCount);
        }

        static long normalizeUtcMs(long utcMs)
        {
            return utcMs > 0L ? utcMs : 0L;
        }

        static int normalizeRemainCount(int remainCount)
        {
            return remainCount < -1 ? -1 : remainCount;
        }

        static int normalizeLimitedRemainCount(int remainCount)
        {
            if (remainCount < 0)
                return -1;

            return remainCount;
        }

        static int normalizeManualRefreshRemainCount(int manualRefreshRemainCount)
        {
            return manualRefreshRemainCount > 0 ? manualRefreshRemainCount : 0;
        }

        static int normalizeChestLevel(int level)
        {
            return level > 0 ? level : 1;
        }

        static int normalizeChestCurrentExp(int currentExp)
        {
            return currentExp > 0 ? currentExp : 0;
        }

        static SHOP_DISCOUNT_TYPE normalizeDiscountType(SHOP_DISCOUNT_TYPE discountType)
        {
            switch (discountType)
            {
                case SHOP_DISCOUNT_TYPE.PER10:
                case SHOP_DISCOUNT_TYPE.PER20:
                case SHOP_DISCOUNT_TYPE.PER30:
                case SHOP_DISCOUNT_TYPE.PER50:
                    return discountType;
                default:
                    return SHOP_DISCOUNT_TYPE.NONE;
            }
        }

        static string normalizeShopItemId(string shopItemId)
        {
            return shopItemId != null ? shopItemId.Trim() : string.Empty;
        }

        static SHOP_CATALOG_TYPE resolveCatalogTypeFromTable(string shopItemId)
        {
            if (string.IsNullOrWhiteSpace(shopItemId))
                return SHOP_CATALOG_TYPE.NONE;

            var normalizedShopItemId = shopItemId.Trim();
            if (TB_SHOP_ITEM_DAILY.Get(normalizedShopItemId) != null)
                return SHOP_CATALOG_TYPE.DAILY;

            if (TB_SHOP_ITEM_EVENT.Get(normalizedShopItemId) != null)
                return SHOP_CATALOG_TYPE.EVENT;

            if (TB_SHOP_ITEM_CHEST.Get(normalizedShopItemId) != null)
                return SHOP_CATALOG_TYPE.CHEST;

            if (TB_SHOP_ITEM_PURCHASE.Get(normalizedShopItemId) != null)
                return SHOP_CATALOG_TYPE.PURCHASE;

            if (TB_SHOP_ITEM_GOLD.Get(normalizedShopItemId) != null)
                return SHOP_CATALOG_TYPE.GOLD;

            return SHOP_CATALOG_TYPE.NONE;
        }
    }
}
