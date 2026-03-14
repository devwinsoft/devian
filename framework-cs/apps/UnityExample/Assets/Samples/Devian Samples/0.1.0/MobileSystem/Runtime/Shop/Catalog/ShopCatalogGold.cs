using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class ShopCatalogGold : ShopCatalogBase
    {
        public ShopCatalogGold(
            ShopStorage storage = null,
            ShopCatalogGoldStorageData storageData = null,
            SHOP_CATALOG catalogConfig = null)
            : this(storage, storageData, products: null, catalogConfig)
        {
        }

        internal ShopCatalogGold(
            ShopStorage storage,
            ShopCatalogGoldStorageData storageData,
            IReadOnlyList<ShopProductBase> products,
            SHOP_CATALOG catalogConfig = null)
            : base(SHOP_CATALOG_TYPE.GOLD, storage, storageData, catalogConfig, products)
        {
        }
    }
}
