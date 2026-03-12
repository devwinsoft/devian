using System;
using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    [Serializable]
    public sealed class ShopStorage
    {
        public int schemaVersion = 4;
        public long lastResetUtcDayStartMs;
        public Dictionary<string, int> purchaseCounts = new();
        public Dictionary<string, long> adsCatalogResetStartedAtUtcMsByCatalog = new();

        public bool TryGetPurchaseCount(string shopId, out int purchaseCount)
        {
            purchaseCount = 0;
            if (string.IsNullOrWhiteSpace(shopId))
                return false;

            return purchaseCounts.TryGetValue(shopId.Trim(), out purchaseCount);
        }

        public int GetPurchaseCount(string shopId)
        {
            if (!TryGetPurchaseCount(shopId, out var purchaseCount))
                return 0;

            return purchaseCount < 0 ? 0 : purchaseCount;
        }

        public void SetPurchaseCount(string shopId, int purchaseCount)
        {
            var key = shopId != null ? shopId.Trim() : string.Empty;
            if (string.IsNullOrEmpty(key))
                return;

            purchaseCounts[key] = purchaseCount < 0 ? 0 : purchaseCount;
        }

        public void IncrementPurchaseCount(string shopId)
        {
            var current = GetPurchaseCount(shopId);
            SetPurchaseCount(shopId, current + 1);
        }

        public void RemovePurchaseCount(string shopId)
        {
            var key = shopId != null ? shopId.Trim() : string.Empty;
            if (string.IsNullOrEmpty(key))
                return;

            purchaseCounts.Remove(key);
        }

        public void ClearPurchaseCounts(IReadOnlyList<string> shopIds)
        {
            if (shopIds == null || shopIds.Count <= 0)
                return;

            for (var i = 0; i < shopIds.Count; i++)
                RemovePurchaseCount(shopIds[i]);
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

        public void SetLastResetUtcDayStartMs(long resetUtcDayStartMs)
        {
            lastResetUtcDayStartMs = resetUtcDayStartMs > 0L ? resetUtcDayStartMs : 0L;
        }

        public void Clear()
        {
            schemaVersion = 4;
            lastResetUtcDayStartMs = 0L;
            purchaseCounts.Clear();
            adsCatalogResetStartedAtUtcMsByCatalog.Clear();
        }

        static string normalizeCatalogKey(SHOP_CATALOG_TYPE catalogType)
        {
            return catalogType == SHOP_CATALOG_TYPE.NONE
                ? string.Empty
                : catalogType.ToString();
        }
    }
}
