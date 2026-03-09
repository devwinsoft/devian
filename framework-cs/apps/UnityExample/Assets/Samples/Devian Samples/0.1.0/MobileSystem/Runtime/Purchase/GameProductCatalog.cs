using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class GameProductCatalog : IPurchaseProductCatalog
    {
        static PurchaseProductType mapProductType(PURCHASE_KIND kind)
        {
            switch (kind)
            {
                case PURCHASE_KIND.SUBSCRIPTION:
                    return PurchaseProductType.Subscription;
                case PURCHASE_KIND.RENTAL:
                    // Rental must stay repurchasable at store level (policy allows repeated purchase).
                    return PurchaseProductType.Consumable;
                case PURCHASE_KIND.CONSUMABLE:
                    return PurchaseProductType.Consumable;
                case PURCHASE_KIND.PASS:
                default:
                    return PurchaseProductType.NonConsumable;
            }
        }

        static string getStoreSku(PURCHASE p)
        {
#if UNITY_IOS || UNITY_TVOS
            return string.IsNullOrEmpty(p.StoreSkuApple) ? p.InternalProductId : p.StoreSkuApple;
#elif UNITY_ANDROID
            return string.IsNullOrEmpty(p.StoreSkuGoogle) ? p.InternalProductId : p.StoreSkuGoogle;
#else
            return p.InternalProductId;
#endif
        }

        public IReadOnlyList<PurchaseCatalogItem> GetActiveProducts()
        {
            var products = TB_PURCHASE.GetAll();
            var list = new List<PurchaseCatalogItem>(products.Count);

            foreach (var p in products)
            {
                if (!p.IsActive) continue;
                list.Add(new PurchaseCatalogItem(p.InternalProductId, getStoreSku(p), mapProductType(p.Kind)));
            }

            return list;
        }
    }
}
