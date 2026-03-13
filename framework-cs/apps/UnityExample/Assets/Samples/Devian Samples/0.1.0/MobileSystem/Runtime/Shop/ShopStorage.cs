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
    public sealed class ShopStorage
    {
        public int schemaVersion = 6;
        public Dictionary<string, int> productRemainCounts = new();
        public Dictionary<string, long> adsCatalogResetStartedAtUtcMsByCatalog = new();
        public List<ShopDailyProductState> dailyCatalogProducts = new();

        [NonSerialized]
        readonly Dictionary<string, int> _legacyPurchaseCounts = new();

        public bool TryGetProductRemainCount(string shopId, out int remainCount)
        {
            remainCount = -1;
            var key = normalizeShopId(shopId);
            if (string.IsNullOrEmpty(key))
                return false;

            return productRemainCounts.TryGetValue(key, out remainCount);
        }

        public void SetProductRemainCount(string shopId, int remainCount)
        {
            var key = normalizeShopId(shopId);
            if (string.IsNullOrEmpty(key))
                return;

            var normalizedRemainCount = normalizeLimitedRemainCount(remainCount);
            if (normalizedRemainCount < 0)
            {
                productRemainCounts.Remove(key);
                return;
            }

            productRemainCounts[key] = normalizedRemainCount;
        }

        public void RemoveProductRemainCount(string shopId)
        {
            var key = normalizeShopId(shopId);
            if (string.IsNullOrEmpty(key))
                return;

            productRemainCounts.Remove(key);
        }

        public void ClearProductRemainCounts(IReadOnlyList<string> shopIds)
        {
            if (shopIds == null || shopIds.Count <= 0)
                return;

            for (var i = 0; i < shopIds.Count; i++)
                RemoveProductRemainCount(shopIds[i]);
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

        public long GetAdsCatalogResetStartedAtUtcMs(SHOP_CATALOG_TYPE catalogType)
        {
            var key = normalizeCatalogKey(catalogType);
            if (string.IsNullOrEmpty(key))
                return 0L;

            if (!adsCatalogResetStartedAtUtcMsByCatalog.TryGetValue(key, out var startedAtUtcMs))
                return 0L;

            return startedAtUtcMs > 0L ? startedAtUtcMs : 0L;
        }

        public void SetAdsCatalogResetStartedAtUtcMs(SHOP_CATALOG_TYPE catalogType, long startedAtUtcMs)
        {
            var key = normalizeCatalogKey(catalogType);
            if (string.IsNullOrEmpty(key))
                return;

            adsCatalogResetStartedAtUtcMsByCatalog[key] = startedAtUtcMs > 0L ? startedAtUtcMs : 0L;
        }

        public void ClearAdsCatalogResetStartedAtUtcMs(SHOP_CATALOG_TYPE catalogType)
        {
            var key = normalizeCatalogKey(catalogType);
            if (string.IsNullOrEmpty(key))
                return;

            adsCatalogResetStartedAtUtcMsByCatalog.Remove(key);
        }

        public IReadOnlyList<ShopDailyProductState> GetDailyCatalogProducts()
        {
            return dailyCatalogProducts;
        }

        public bool TryGetDailyCatalogProduct(string shopId, out ShopDailyProductState state)
        {
            state = null;
            var key = normalizeShopId(shopId);
            if (string.IsNullOrEmpty(key))
                return false;

            for (var i = 0; i < dailyCatalogProducts.Count; i++)
            {
                var item = dailyCatalogProducts[i];
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
            dailyCatalogProducts.Clear();
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

                dailyCatalogProducts.Add(createDailyState(key, state.discountType, state.remainCount));
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

            var normalizedState = createDailyState(key, discountType, remainCount);
            for (var i = 0; i < dailyCatalogProducts.Count; i++)
            {
                var item = dailyCatalogProducts[i];
                if (item == null)
                    continue;

                if (!string.Equals(normalizeShopId(item.shopId), key, StringComparison.Ordinal))
                    continue;

                dailyCatalogProducts[i] = normalizedState;
                return;
            }

            dailyCatalogProducts.Add(normalizedState);
        }

        public void RemoveDailyCatalogProduct(string shopId)
        {
            var key = normalizeShopId(shopId);
            if (string.IsNullOrEmpty(key))
                return;

            for (var i = dailyCatalogProducts.Count - 1; i >= 0; i--)
            {
                var item = dailyCatalogProducts[i];
                if (item == null)
                {
                    dailyCatalogProducts.RemoveAt(i);
                    continue;
                }

                if (string.Equals(normalizeShopId(item.shopId), key, StringComparison.Ordinal))
                    dailyCatalogProducts.RemoveAt(i);
            }
        }

        public void ClearDailyCatalogProducts()
        {
            dailyCatalogProducts.Clear();
        }

        public void Clear()
        {
            schemaVersion = 6;
            productRemainCounts.Clear();
            adsCatalogResetStartedAtUtcMsByCatalog.Clear();
            dailyCatalogProducts.Clear();
            _legacyPurchaseCounts.Clear();
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
    }
}
