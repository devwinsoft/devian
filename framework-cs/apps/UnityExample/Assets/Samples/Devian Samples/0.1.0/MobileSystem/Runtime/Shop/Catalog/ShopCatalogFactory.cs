using System;
using System.Collections.Generic;
using Devian.Domain.Game;
using UnityEngine;

namespace Devian
{
    internal static class ShopCatalogFactory
    {
        public static IReadOnlyList<ShopCatalogBase> CreateRuntimeCatalogs(ShopStorage storage)
        {
            var rows = TB_SHOP_CATALOG.GetAll();
            if (rows == null || rows.Count <= 0)
                return Array.Empty<ShopCatalogBase>();

            var catalogs = new List<ShopCatalogBase>(rows.Count);
            var seenCatalogTypes = new HashSet<SHOP_CATALOG_TYPE>();
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null || row.CatalogType == SHOP_CATALOG_TYPE.NONE)
                    continue;

                if (!seenCatalogTypes.Add(row.CatalogType))
                {
                    Debug.LogWarning(
                        $"[ShopCatalogFactory] Duplicate SHOP_CATALOG row. Keeping first row: catalog={row.CatalogType}");
                    continue;
                }

                var catalog = createCatalog(row.CatalogType, storage, row, products: null);
                if (catalog != null)
                    catalogs.Add(catalog);
            }

            return catalogs;
        }

        public static ShopCatalogBase Create(SHOP_CATALOG_TYPE catalogType)
        {
            var catalog = createCatalog(
                catalogType,
                storage: null,
                TB_SHOP_CATALOG.Get(catalogType),
                products: null);

            initializeCatalog(catalog);
            return catalog;
        }

        public static ShopCatalogBase Create(SHOP_CATALOG_TYPE catalogType, IReadOnlyList<ShopProductBase> products)
        {
            var catalog = createCatalog(
                catalogType,
                storage: null,
                TB_SHOP_CATALOG.Get(catalogType),
                products);

            initializeCatalog(catalog);
            return catalog;
        }

        public static ShopCatalogBase Create(ShopCatalogBase sourceCatalog, IReadOnlyList<ShopProductBase> products)
        {
            if (sourceCatalog == null)
                return Empty(SHOP_CATALOG_TYPE.NONE);

            var catalog = createCatalog(
                sourceCatalog.CatalogType,
                storage: null,
                sourceCatalog.CatalogConfig,
                products);
            catalog.SetLocked(sourceCatalog.IsLocked);

            initializeCatalog(catalog);
            return catalog;
        }

        public static ShopCatalogBase Empty(SHOP_CATALOG_TYPE catalogType)
        {
            var catalog = new ShopCatalogEmpty(catalogType, storage: null, catalogConfig: TB_SHOP_CATALOG.Get(catalogType));
            initializeCatalog(catalog);
            return catalog;
        }

        static ShopCatalogBase createCatalog(
            SHOP_CATALOG_TYPE catalogType,
            ShopStorage storage,
            SHOP_CATALOG catalogConfig,
            IReadOnlyList<ShopProductBase> products)
        {
            var storageData = storage?.GetCatalogData(catalogType);
            if (products == null)
            {
                return catalogType switch
                {
                    SHOP_CATALOG_TYPE.DAILY => new ShopCatalogDaily(storage, storageData as ShopCatalogDailyStorageData, catalogConfig),
                    SHOP_CATALOG_TYPE.EVENT => new ShopCatalogEvent(storage, storageData as ShopCatalogEventStorageData, catalogConfig),
                    SHOP_CATALOG_TYPE.CHEST => new ShopCatalogChest(storage, storageData as ShopCatalogChestStorageData, catalogConfig),
                    SHOP_CATALOG_TYPE.PURCHASE => new ShopCatalogPurchase(storage, storageData as ShopCatalogPurchaseStorageData, catalogConfig),
                    SHOP_CATALOG_TYPE.GOLD => new ShopCatalogGold(storage, storageData as ShopCatalogGoldStorageData, catalogConfig),
                    _ => new ShopCatalogEmpty(catalogType, storage, storageData, catalogConfig),
                };
            }

            return catalogType switch
            {
                SHOP_CATALOG_TYPE.DAILY => new ShopCatalogDaily(storage, storageData as ShopCatalogDailyStorageData, products, catalogConfig),
                SHOP_CATALOG_TYPE.EVENT => new ShopCatalogEvent(storage, storageData as ShopCatalogEventStorageData, products, catalogConfig),
                SHOP_CATALOG_TYPE.CHEST => new ShopCatalogChest(storage, storageData as ShopCatalogChestStorageData, products, catalogConfig),
                SHOP_CATALOG_TYPE.PURCHASE => new ShopCatalogPurchase(storage, storageData as ShopCatalogPurchaseStorageData, products, catalogConfig),
                SHOP_CATALOG_TYPE.GOLD => new ShopCatalogGold(storage, storageData as ShopCatalogGoldStorageData, products, catalogConfig),
                _ => new ShopCatalogEmpty(catalogType, storage, storageData, catalogConfig),
            };
        }

        static void initializeCatalog(ShopCatalogBase catalog)
        {
            catalog?.Initialize();
        }
    }
}
