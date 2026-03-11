using System;
using System.Collections.Generic;

namespace Devian
{
    [Serializable]
    public sealed class ShopStorage
    {
        public int schemaVersion = 1;
        public Dictionary<string, ShopPurchaseLimitState> purchaseLimits = new();

        public bool TryGetPurchaseLimit(string productId, out ShopPurchaseLimitState state)
        {
            state = null;
            if (string.IsNullOrWhiteSpace(productId))
                return false;

            return purchaseLimits.TryGetValue(productId.Trim(), out state);
        }

        public ShopPurchaseLimitState GetOrCreatePurchaseLimit(string productId)
        {
            var key = productId != null ? productId.Trim() : string.Empty;
            if (string.IsNullOrEmpty(key))
                return null;

            if (!purchaseLimits.TryGetValue(key, out var state) || state == null)
            {
                state = new ShopPurchaseLimitState();
                purchaseLimits[key] = state;
            }

            return state;
        }

        public void Clear()
        {
            schemaVersion = 1;
            purchaseLimits.Clear();
        }
    }

    [Serializable]
    public sealed class ShopPurchaseLimitState
    {
        public long periodStartUtcMs;
        public int purchaseCount;
    }
}
