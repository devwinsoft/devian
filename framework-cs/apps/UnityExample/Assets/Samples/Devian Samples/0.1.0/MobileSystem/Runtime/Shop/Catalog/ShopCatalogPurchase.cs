using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class ShopCatalogPurchase : ShopCatalogBase
    {
        public ShopCatalogPurchase(ShopStorage storage = null, SHOP_CATALOG catalogConfig = null)
            : this(storage, products: null, catalogConfig)
        {
        }

        internal ShopCatalogPurchase(ShopStorage storage, IReadOnlyList<ShopProductBase> products, SHOP_CATALOG catalogConfig = null)
            : base(SHOP_CATALOG_TYPE.PURCHASE, storage, catalogConfig, products)
        {
        }
    }
}
