using System;
using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    [Serializable]
    public sealed class ShopDailyProductState
    {
        public string shopId = string.Empty;
        public SHOP_DISCOUNT_TYPE discountType = SHOP_DISCOUNT_TYPE.NONE;
        public int remainCount = -1;
    }

    [Serializable]
    public sealed class ShopCatalogStorageState
    {
        public long autoRefreshUtcMs;
        public long adsRefreshUtcMs;
        public long manualRefreshUtcMs;
        public int manualRefreshCount;
        public Dictionary<string, int> productRemainCounts = new();
        public List<ShopDailyProductState> dailyCatalogProducts = new();
    }

    [Serializable]
    public sealed class ShopStorage
    {
        public int schemaVersion = 10;
        public Dictionary<string, ShopCatalogStorageState> catalogs = new();

        [NonSerialized]
        readonly Dictionary<string, int> _legacyPurchaseCounts = new();

        public bool TryGetProductRemainCount(
            SHOP_CATALOG_TYPE catalogType,
            string shopId,
            out int remainCount)
        {
            remainCount = -1;
            var state = getCatalogState(catalogType, createIfMissing: false);
            if (state == null)
                return false;

            var key = normalizeShopId(shopId);
            if (string.IsNullOrEmpty(key))
                return false;

            return state.productRemainCounts.TryGetValue(key, out remainCount);
        }

        public void SetProductRemainCount(
            SHOP_CATALOG_TYPE catalogType,
            string shopId,
            int remainCount)
        {
            var key = normalizeShopId(shopId);
            if (string.IsNullOrEmpty(key))
                return;

            var state = getCatalogState(catalogType, createIfMissing: true);
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

        public void RemoveProductRemainCount(SHOP_CATALOG_TYPE catalogType, string shopId)
        {
            var state = getCatalogState(catalogType, createIfMissing: false);
            if (state == null)
                return;

            var key = normalizeShopId(shopId);
            if (string.IsNullOrEmpty(key))
                return;

            state.productRemainCounts.Remove(key);
        }

        public void ClearProductRemainCounts(
            SHOP_CATALOG_TYPE catalogType,
            IReadOnlyList<string> shopIds)
        {
            var state = getCatalogState(catalogType, createIfMissing: false);
            if (state == null || shopIds == null || shopIds.Count <= 0)
                return;

            for (var i = 0; i < shopIds.Count; i++)
            {
                var key = normalizeShopId(shopIds[i]);
                if (string.IsNullOrEmpty(key))
                    continue;

                state.productRemainCounts.Remove(key);
            }
        }

        // Backward-compatible wrapper. Prefer catalog-aware overload.
        public bool TryGetProductRemainCount(string shopId, out int remainCount)
        {
            remainCount = -1;
            var key = normalizeShopId(shopId);
            if (string.IsNullOrEmpty(key) || catalogs == null || catalogs.Count <= 0)
                return false;

            foreach (var kv in catalogs)
            {
                var state = ensureCatalogStateFields(kv.Value);
                if (state.productRemainCounts.TryGetValue(key, out remainCount))
                    return true;
            }

            return false;
        }

        // Backward-compatible wrapper. Prefer catalog-aware overload.
        public void SetProductRemainCount(string shopId, int remainCount)
        {
            var catalogType = resolveCatalogTypeFromTable(shopId);
            if (catalogType == SHOP_CATALOG_TYPE.NONE)
                return;

            SetProductRemainCount(catalogType, shopId, remainCount);
        }

        // Backward-compatible wrapper. Prefer catalog-aware overload.
        public void RemoveProductRemainCount(string shopId)
        {
            var key = normalizeShopId(shopId);
            if (string.IsNullOrEmpty(key) || catalogs == null || catalogs.Count <= 0)
                return;

            foreach (var kv in catalogs)
            {
                var state = ensureCatalogStateFields(kv.Value);
                state.productRemainCounts.Remove(key);
            }
        }

        // Backward-compatible wrapper. Prefer catalog-aware overload.
        public void ClearProductRemainCounts(IReadOnlyList<string> shopIds)
        {
            if (shopIds == null || shopIds.Count <= 0 || catalogs == null || catalogs.Count <= 0)
                return;

            foreach (var kv in catalogs)
            {
                var state = ensureCatalogStateFields(kv.Value);
                for (var i = 0; i < shopIds.Count; i++)
                {
                    var key = normalizeShopId(shopIds[i]);
                    if (string.IsNullOrEmpty(key))
                        continue;

                    state.productRemainCounts.Remove(key);
                }
            }
        }

        internal void SetLegacyPurchaseCount(string shopId, int purchaseCount)
        {
            var key = normalizeShopId(shopId);
            if (string.IsNullOrEmpty(key))
                return;

            _legacyPurchaseCounts[key] = purchaseCount < 0 ? 0 : purchaseCount;
        }

        internal bool TryTakeLegacyPurchaseCount(string shopId, out int purchaseCount)
        {
            purchaseCount = 0;
            var key = normalizeShopId(shopId);
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
            var state = getCatalogState(catalogType, createIfMissing: false);
            if (state == null)
                return 0L;

            return state.autoRefreshUtcMs > 0L ? state.autoRefreshUtcMs : 0L;
        }

        public void SetAutoRefreshUtcMs(SHOP_CATALOG_TYPE catalogType, long utcMs)
        {
            var state = getCatalogState(catalogType, createIfMissing: true);
            if (state == null)
                return;

            state.autoRefreshUtcMs = utcMs > 0L ? utcMs : 0L;
        }

        public void ClearAutoRefreshUtcMs(SHOP_CATALOG_TYPE catalogType)
        {
            var state = getCatalogState(catalogType, createIfMissing: false);
            if (state == null)
                return;

            state.autoRefreshUtcMs = 0L;
        }

        public long GetAdsRefreshUtcMs(SHOP_CATALOG_TYPE catalogType)
        {
            var state = getCatalogState(catalogType, createIfMissing: false);
            if (state == null)
                return 0L;

            return state.adsRefreshUtcMs > 0L ? state.adsRefreshUtcMs : 0L;
        }

        public void SetAdsRefreshUtcMs(SHOP_CATALOG_TYPE catalogType, long utcMs)
        {
            var state = getCatalogState(catalogType, createIfMissing: true);
            if (state == null)
                return;

            state.adsRefreshUtcMs = utcMs > 0L ? utcMs : 0L;
        }

        public void ClearAdsRefreshUtcMs(SHOP_CATALOG_TYPE catalogType)
        {
            var state = getCatalogState(catalogType, createIfMissing: false);
            if (state == null)
                return;

            state.adsRefreshUtcMs = 0L;
        }

        public long GetManualRefreshUtcMs(SHOP_CATALOG_TYPE catalogType)
        {
            var state = getCatalogState(catalogType, createIfMissing: false);
            if (state == null)
                return 0L;

            return state.manualRefreshUtcMs > 0L ? state.manualRefreshUtcMs : 0L;
        }

        public void SetManualRefreshUtcMs(SHOP_CATALOG_TYPE catalogType, long utcMs)
        {
            var state = getCatalogState(catalogType, createIfMissing: true);
            if (state == null)
                return;

            state.manualRefreshUtcMs = utcMs > 0L ? utcMs : 0L;
        }

        public void ClearManualRefreshUtcMs(SHOP_CATALOG_TYPE catalogType)
        {
            var state = getCatalogState(catalogType, createIfMissing: false);
            if (state == null)
                return;

            state.manualRefreshUtcMs = 0L;
        }

        public int GetManualRefreshCount(SHOP_CATALOG_TYPE catalogType)
        {
            var state = getCatalogState(catalogType, createIfMissing: false);
            if (state == null)
                return 0;

            return normalizeManualRefreshCount(state.manualRefreshCount);
        }

        public void SetManualRefreshCount(SHOP_CATALOG_TYPE catalogType, int manualRefreshCount)
        {
            var state = getCatalogState(catalogType, createIfMissing: true);
            if (state == null)
                return;

            state.manualRefreshCount = normalizeManualRefreshCount(manualRefreshCount);
        }

        public void ClearManualRefreshCount(SHOP_CATALOG_TYPE catalogType)
        {
            var state = getCatalogState(catalogType, createIfMissing: false);
            if (state == null)
                return;

            state.manualRefreshCount = 0;
        }

        public IReadOnlyList<ShopDailyProductState> GetDailyCatalogProducts()
        {
            var daily = getCatalogState(SHOP_CATALOG_TYPE.DAILY, createIfMissing: false);
            return daily != null ? daily.dailyCatalogProducts : Array.Empty<ShopDailyProductState>();
        }

        public bool TryGetDailyCatalogProduct(string shopId, out ShopDailyProductState state)
        {
            state = null;
            var daily = getCatalogState(SHOP_CATALOG_TYPE.DAILY, createIfMissing: false);
            if (daily == null)
                return false;

            var key = normalizeShopId(shopId);
            if (string.IsNullOrEmpty(key))
                return false;

            var dailyProducts = daily.dailyCatalogProducts;
            for (var i = 0; i < dailyProducts.Count; i++)
            {
                var item = dailyProducts[i];
                if (item == null)
                    continue;

                if (!string.Equals(normalizeShopId(item.shopId), key, StringComparison.Ordinal))
                    continue;

                state = cloneDailyState(item);
                return true;
            }

            return false;
        }

        public void SetDailyCatalogProducts(IReadOnlyList<ShopDailyProductState> states)
        {
            var daily = getCatalogState(SHOP_CATALOG_TYPE.DAILY, createIfMissing: true);
            if (daily == null)
                return;

            daily.dailyCatalogProducts.Clear();
            if (states == null || states.Count <= 0)
                return;

            var seenShopIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (state == null)
                    continue;

                var key = normalizeShopId(state.shopId);
                if (string.IsNullOrEmpty(key))
                    continue;

                if (!seenShopIds.Add(key))
                    continue;

                daily.dailyCatalogProducts.Add(createDailyState(key, state.discountType, state.remainCount));
            }
        }

        public void UpsertDailyCatalogProduct(
            string shopId,
            SHOP_DISCOUNT_TYPE discountType,
            int remainCount)
        {
            var key = normalizeShopId(shopId);
            if (string.IsNullOrEmpty(key))
                return;

            var daily = getCatalogState(SHOP_CATALOG_TYPE.DAILY, createIfMissing: true);
            if (daily == null)
                return;

            var normalizedState = createDailyState(key, discountType, remainCount);
            var dailyProducts = daily.dailyCatalogProducts;
            for (var i = 0; i < dailyProducts.Count; i++)
            {
                var item = dailyProducts[i];
                if (item == null)
                    continue;

                if (!string.Equals(normalizeShopId(item.shopId), key, StringComparison.Ordinal))
                    continue;

                dailyProducts[i] = normalizedState;
                return;
            }

            dailyProducts.Add(normalizedState);
        }

        public void RemoveDailyCatalogProduct(string shopId)
        {
            var daily = getCatalogState(SHOP_CATALOG_TYPE.DAILY, createIfMissing: false);
            if (daily == null)
                return;

            var key = normalizeShopId(shopId);
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

                if (string.Equals(normalizeShopId(item.shopId), key, StringComparison.Ordinal))
                    dailyProducts.RemoveAt(i);
            }
        }

        public void ClearDailyCatalogProducts()
        {
            var daily = getCatalogState(SHOP_CATALOG_TYPE.DAILY, createIfMissing: false);
            if (daily == null)
                return;

            daily.dailyCatalogProducts.Clear();
        }

        public void Clear()
        {
            schemaVersion = 10;
            catalogs.Clear();
            _legacyPurchaseCounts.Clear();
        }

        ShopCatalogStorageState getCatalogState(SHOP_CATALOG_TYPE catalogType, bool createIfMissing)
        {
            var key = normalizeCatalogKey(catalogType);
            if (string.IsNullOrEmpty(key))
                return null;

            if (!catalogs.TryGetValue(key, out var state) || state == null)
            {
                if (!createIfMissing)
                    return null;

                state = new ShopCatalogStorageState();
                catalogs[key] = state;
            }

            state = ensureCatalogStateFields(state);
            catalogs[key] = state;
            return state;
        }

        static ShopCatalogStorageState ensureCatalogStateFields(ShopCatalogStorageState state)
        {
            state ??= new ShopCatalogStorageState();
            state.productRemainCounts ??= new Dictionary<string, int>();
            state.dailyCatalogProducts ??= new List<ShopDailyProductState>();
            return state;
        }

        static ShopDailyProductState createDailyState(
            string shopId,
            SHOP_DISCOUNT_TYPE discountType,
            int remainCount)
        {
            return new ShopDailyProductState
            {
                shopId = normalizeShopId(shopId),
                discountType = normalizeDiscountType(discountType),
                remainCount = normalizeRemainCount(remainCount),
            };
        }

        static ShopDailyProductState cloneDailyState(ShopDailyProductState state)
        {
            if (state == null)
                return null;

            return createDailyState(state.shopId, state.discountType, state.remainCount);
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

        static int normalizeManualRefreshCount(int manualRefreshCount)
        {
            return manualRefreshCount > 0 ? manualRefreshCount : 0;
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

        static string normalizeShopId(string shopId)
        {
            return shopId != null ? shopId.Trim() : string.Empty;
        }

        static string normalizeCatalogKey(SHOP_CATALOG_TYPE catalogType)
        {
            return catalogType == SHOP_CATALOG_TYPE.NONE
                ? string.Empty
                : catalogType.ToString();
        }

        static SHOP_CATALOG_TYPE resolveCatalogTypeFromTable(string shopId)
        {
            var key = normalizeShopId(shopId);
            if (string.IsNullOrEmpty(key))
                return SHOP_CATALOG_TYPE.NONE;

            if (TB_SHOP_DAILY.Get(key) != null)
                return SHOP_CATALOG_TYPE.DAILY;

            if (TB_SHOP_EVENT.Get(key) != null)
                return SHOP_CATALOG_TYPE.EVENT;

            if (TB_SHOP_CHEST.Get(key) != null)
                return SHOP_CATALOG_TYPE.CHEST;

            if (TB_SHOP_PURCHASE.Get(key) != null)
                return SHOP_CATALOG_TYPE.PURCHASE;

            if (TB_SHOP_GOLD.Get(key) != null)
                return SHOP_CATALOG_TYPE.GOLD;

            return SHOP_CATALOG_TYPE.NONE;
        }
    }
}
