using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class ShopCatalogGold : ShopCatalogBase
    {
        public ShopCatalogGold(ShopStorage storage = null, SHOP_CATALOG catalogConfig = null)
            : this(storage, products: null, catalogConfig)
        {
        }

        internal ShopCatalogGold(ShopStorage storage, IReadOnlyList<ShopProductBase> products, SHOP_CATALOG catalogConfig = null)
            : base(SHOP_CATALOG_TYPE.GOLD, storage, catalogConfig, products)
        {
        }
    }
}
