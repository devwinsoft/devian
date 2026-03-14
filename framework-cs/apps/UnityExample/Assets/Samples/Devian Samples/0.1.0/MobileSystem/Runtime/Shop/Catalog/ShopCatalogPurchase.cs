using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class ShopCatalogPurchase : ShopCatalogBase
    {
        public ShopCatalogPurchase(
            ShopStorage storage = null,
            ShopCatalogPurchaseStorageData storageData = null,
            SHOP_CATALOG catalogConfig = null)
            : this(storage, storageData, products: null, catalogConfig)
        {
        }

        internal ShopCatalogPurchase(
            ShopStorage storage,
            ShopCatalogPurchaseStorageData storageData,
            IReadOnlyList<ShopProductBase> products,
            SHOP_CATALOG catalogConfig = null)
            : base(SHOP_CATALOG_TYPE.PURCHASE, storage, storageData, catalogConfig, products)
        {
        }
    }
}
