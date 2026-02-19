using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class GameProductCatalog : IPurchaseProductCatalog
    {
        static PurchaseProductType mapProductType(ProductKind kind)
        {
            switch (kind)
            {
                case ProductKind.Subscription:
                    return PurchaseProductType.Subscription;
                case ProductKind.Consumable:
                    return PurchaseProductType.Consumable;
                case ProductKind.Rental:
                case ProductKind.SeasonPass:
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
