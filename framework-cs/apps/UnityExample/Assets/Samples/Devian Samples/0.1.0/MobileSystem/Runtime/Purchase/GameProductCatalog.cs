using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class GameProductCatalog : IPurchaseProductCatalog
    {
        static PurchaseProductType mapProductType(PRODUCT_KIND kind)
        {
            switch (kind)
            {
                case PRODUCT_KIND.SUBSCRIPTION:
                    return PurchaseProductType.Subscription;
                case PRODUCT_KIND.RENTAL:
                    // Rental must stay repurchasable at store level (policy allows repeated purchase).
                    return PurchaseProductType.Consumable;
                case PRODUCT_KIND.CONSUMABLE:
                    return PurchaseProductType.Consumable;
                case PRODUCT_KIND.SEASON_PASS:
                default:
                    return PurchaseProductType.NonConsumable;
            }
        }

        static string getStoreSku(PRODUCT p)
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
            var products = TB_PRODUCT.GetAll();
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
