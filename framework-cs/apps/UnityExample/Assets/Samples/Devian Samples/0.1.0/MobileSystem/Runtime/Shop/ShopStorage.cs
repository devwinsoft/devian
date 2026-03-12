using System;
using System.Collections.Generic;

namespace Devian
{
    [Serializable]
    public sealed class ShopStorage
    {
        public int schemaVersion = 2;
        public long lastResetUtcDayStartMs;
        public Dictionary<string, int> purchaseCounts = new();

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

        public void ResetDaily(long resetUtcDayStartMs)
        {
            lastResetUtcDayStartMs = resetUtcDayStartMs > 0L ? resetUtcDayStartMs : 0L;
            purchaseCounts.Clear();
        }

        public void Clear()
        {
            schemaVersion = 2;
            lastResetUtcDayStartMs = 0L;
            purchaseCounts.Clear();
        }
    }
}
