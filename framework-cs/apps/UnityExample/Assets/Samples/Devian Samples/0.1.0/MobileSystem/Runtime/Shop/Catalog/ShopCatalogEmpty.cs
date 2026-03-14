using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    sealed class ShopCatalogEmpty : ShopCatalogBase
    {
        public ShopCatalogEmpty(
            SHOP_CATALOG_TYPE catalogType,
            ShopStorage storage = null,
            ShopCatalogStorageDataBase storageData = null,
            SHOP_CATALOG catalogConfig = null)
            : base(catalogType, storage, storageData, catalogConfig)
        {
        }
    }
}
