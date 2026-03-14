using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class ShopCatalogChest : ShopCatalogBase
    {
        public ShopCatalogChest(
            ShopStorage storage = null,
            ShopCatalogChestStorageData storageData = null,
            SHOP_CATALOG catalogConfig = null)
            : this(storage, storageData, products: null, catalogConfig)
        {
        }

        internal ShopCatalogChest(
            ShopStorage storage,
            ShopCatalogChestStorageData storageData,
            IReadOnlyList<ShopProductBase> products,
            SHOP_CATALOG catalogConfig = null)
            : base(SHOP_CATALOG_TYPE.CHEST, storage, storageData, catalogConfig, products)
        {
        }
    }
}
