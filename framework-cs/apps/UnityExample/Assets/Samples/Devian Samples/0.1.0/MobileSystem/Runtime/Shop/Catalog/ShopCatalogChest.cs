using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class ShopCatalogChest : ShopCatalogBase
    {
        public ShopCatalogChest(ShopStorage storage = null, SHOP_CATALOG catalogConfig = null)
            : this(storage, products: null, catalogConfig)
        {
        }

        internal ShopCatalogChest(ShopStorage storage, IReadOnlyList<ShopProductBase> products, SHOP_CATALOG catalogConfig = null)
            : base(SHOP_CATALOG_TYPE.CHEST, storage, catalogConfig, products)
        {
        }
    }
}
