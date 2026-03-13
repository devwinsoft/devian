using System;
using System.Collections.Generic;
using Devian.Domain.Game;
using UnityEngine;

namespace Devian
{
    public abstract class ShopCatalogBase
    {
        static readonly IReadOnlyList<ShopProductBase> EmptyProducts = Array.Empty<ShopProductBase>();

        IReadOnlyList<ShopProductBase> _products = EmptyProducts;
        readonly Dictionary<string, ShopProductBase> _productsByShopId = new(StringComparer.Ordinal);
        bool _initialized;

        protected ShopCatalogBase(SHOP_CATALOG_TYPE catalogType)
        {
            CatalogType = catalogType;
        }

        public SHOP_CATALOG_TYPE CatalogType { get; }

        public void Initialize()
        {
            if (_initialized)
                return;

            setProducts(onInitialize());
            _initialized = true;
        }

        protected abstract IReadOnlyList<ShopProductBase> onInitialize();

        public IReadOnlyList<ShopProductBase> GetProducts()
        {
            Initialize();
            return _products;
        }

        public ShopProductBase GetProduct(string shopId)
        {
            Initialize();
            var normalizedShopId = NormalizeShopId(shopId);
            if (string.IsNullOrEmpty(normalizedShopId))
                return null;

            return _productsByShopId.TryGetValue(normalizedShopId, out var product)
                ? product
                : null;
        }

        protected static string NormalizeShopId(string shopId)
        {
            return shopId != null ? shopId.Trim() : string.Empty;
        }

        protected static IReadOnlyList<ShopProductBase> BuildProductsFromRows<TRow>(
            IReadOnlyList<TRow> rows,
            Func<TRow, ShopProductBase> createProduct)
        {
            if (rows == null || rows.Count <= 0 || createProduct == null)
                return EmptyProducts;

            var products = new List<ShopProductBase>(rows.Count);
            for (var i = 0; i < rows.Count; i++)
            {
                var product = createProduct(rows[i]);
                if (product != null)
                    products.Add(product);
            }

            return products;
        }

        public static IReadOnlyList<ShopCatalogBase> CreateDefaultCatalogs(ShopStorage storage)
        {
            var catalogs = new ShopCatalogBase[]
            {
                new ShopCatalogDaily(storage),
                new ShopCatalogChest(),
                new ShopCatalogPurchase(),
                new ShopCatalogGold(),
            };

            return initializeCatalogs(catalogs);
        }

        public static ShopCatalogBase Create(SHOP_CATALOG_TYPE catalogType)
        {
            var catalog = catalogType switch
            {
                SHOP_CATALOG_TYPE.DAILY => new ShopCatalogDaily(),
                SHOP_CATALOG_TYPE.CHEST => new ShopCatalogChest(),
                SHOP_CATALOG_TYPE.PURCHASE => new ShopCatalogPurchase(),
                SHOP_CATALOG_TYPE.GOLD => new ShopCatalogGold(),
                _ => Empty(catalogType),
            };

            initializeCatalog(catalog);
            return catalog;
        }

        public static ShopCatalogBase Create(SHOP_CATALOG_TYPE catalogType, IReadOnlyList<ShopProductBase> products)
        {
            var catalog = catalogType switch
            {
                SHOP_CATALOG_TYPE.DAILY => new ShopCatalogDaily(products),
                SHOP_CATALOG_TYPE.CHEST => new ShopCatalogChest(products),
                SHOP_CATALOG_TYPE.PURCHASE => new ShopCatalogPurchase(products),
                SHOP_CATALOG_TYPE.GOLD => new ShopCatalogGold(products),
                _ => Empty(catalogType),
            };

            initializeCatalog(catalog);
            return catalog;
        }

        public static ShopCatalogBase Empty(SHOP_CATALOG_TYPE catalogType)
        {
            var catalog = new ShopCatalogEmpty(catalogType);
            initializeCatalog(catalog);
            return catalog;
        }

        static IReadOnlyList<ShopCatalogBase> initializeCatalogs(IReadOnlyList<ShopCatalogBase> catalogs)
        {
            if (catalogs == null || catalogs.Count <= 0)
                return Array.Empty<ShopCatalogBase>();

            for (var i = 0; i < catalogs.Count; i++)
                initializeCatalog(catalogs[i]);

            return catalogs;
        }

        static void initializeCatalog(ShopCatalogBase catalog)
        {
            catalog?.Initialize();
        }

        void setProducts(IReadOnlyList<ShopProductBase> products)
        {
            _products = products ?? EmptyProducts;
            _productsByShopId.Clear();

            for (var i = 0; i < _products.Count; i++)
            {
                var product = _products[i];
                if (product == null)
                    continue;

                var normalizedShopId = NormalizeShopId(product.ShopId);
                if (string.IsNullOrEmpty(normalizedShopId))
                    continue;

                if (_productsByShopId.ContainsKey(normalizedShopId))
                {
                    Debug.LogWarning(
                        $"[ShopCatalogBase] Duplicate shopId in catalog. Keeping first row: catalog={CatalogType}, shopId={normalizedShopId}");
                    continue;
                }

                _productsByShopId.Add(normalizedShopId, product);
            }
        }
    }

    sealed class ShopCatalogEmpty : ShopCatalogBase
    {
        public ShopCatalogEmpty(SHOP_CATALOG_TYPE catalogType)
            : base(catalogType)
        {
        }

        protected override IReadOnlyList<ShopProductBase> onInitialize()
        {
            return Array.Empty<ShopProductBase>();
        }
    }

    public sealed class ShopCatalogDaily : ShopCatalogBase
    {
        const int DailySelectableProductCount = 5;
        const int DailyDiscountProductCount = 3;
        readonly ShopStorage _storage;
        readonly IReadOnlyList<ShopProductBase> _prebuiltProducts;

        public ShopCatalogDaily()
            : this(storage: null, products: null)
        {
        }

        public ShopCatalogDaily(ShopStorage storage)
            : this(storage, products: null)
        {
        }

        internal ShopCatalogDaily(IReadOnlyList<ShopProductBase> products)
            : this(storage: null, products: products)
        {
        }

        ShopCatalogDaily(ShopStorage storage, IReadOnlyList<ShopProductBase> products)
            : base(SHOP_CATALOG_TYPE.DAILY)
        {
            _storage = storage;
            _prebuiltProducts = products;
        }

        protected override IReadOnlyList<ShopProductBase> onInitialize()
        {
            if (_prebuiltProducts != null)
                return _prebuiltProducts;

            return loadOrCreateDailyProducts(_storage);
        }

        static IReadOnlyList<ShopProductBase> loadOrCreateDailyProducts(ShopStorage storage)
        {
            if (tryBuildDailyProductsFromStorage(storage, out var storedProducts))
                return storedProducts;

            var rows = TB_SHOP_DAILY.GetAll();
            var products = createDailyProductsFromRows(rows, DailySelectableProductCount, DailyDiscountProductCount);

            if (storage != null)
            {
                var states = createDailyProductStates(products);
                storage.SetDailyCatalogProducts(states);
            }

            return products;
        }

        static bool tryBuildDailyProductsFromStorage(ShopStorage storage, out IReadOnlyList<ShopProductBase> products)
        {
            products = null;
            if (storage == null)
                return false;

            var states = storage.GetDailyCatalogProducts();
            if (states == null || states.Count <= 0)
                return false;

            var list = new List<ShopProductBase>(states.Count);
            var seenShopIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (state == null)
                    return false;

                var normalizedShopId = NormalizeShopId(state.shopId);
                if (string.IsNullOrEmpty(normalizedShopId))
                    return false;

                if (!seenShopIds.Add(normalizedShopId))
                    return false;

                var row = TB_SHOP_DAILY.Get(normalizedShopId);
                if (row == null)
                    return false;

                var product = ShopProductFactory.CreateDailyProduct(row, normalizeDiscountType(state.discountType));
                if (product == null)
                    return false;

                product.SetRemainCount(state.remainCount);
                list.Add(product);
            }

            if (list.Count <= 0)
                return false;

            products = list;
            return true;
        }

        static List<ShopDailyProductState> createDailyProductStates(IReadOnlyList<ShopProductBase> products)
        {
            var result = new List<ShopDailyProductState>(products?.Count ?? 0);
            if (products == null || products.Count <= 0)
                return result;

            var seenShopIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < products.Count; i++)
            {
                var product = products[i];
                if (product == null || product.CatalogType != SHOP_CATALOG_TYPE.DAILY)
                    continue;

                var normalizedShopId = NormalizeShopId(product.ShopId);
                if (string.IsNullOrEmpty(normalizedShopId))
                    continue;

                if (!seenShopIds.Add(normalizedShopId))
                    continue;

                result.Add(new ShopDailyProductState
                {
                    shopId = normalizedShopId,
                    discountType = product.DiscountType,
                    remainCount = product.RemainCount,
                });
            }

            return result;
        }

        static IReadOnlyList<ShopProductBase> createDailyProductsFromRows(
            IReadOnlyList<SHOP_DAILY> rows,
            int targetCount,
            int targetDiscountCount)
        {
            var selectedRows = selectDailyRows(rows, targetCount);
            var discountTypesByShopId = selectDailyDiscountTypes(selectedRows, targetDiscountCount);
            var products = new List<ShopProductBase>(selectedRows.Count);
            for (var i = 0; i < selectedRows.Count; i++)
            {
                var row = selectedRows[i];
                if (row == null)
                    continue;

                var normalizedShopId = NormalizeShopId(row.ShopId);
                var discountType = SHOP_DISCOUNT_TYPE.NONE;
                if (!string.IsNullOrEmpty(normalizedShopId))
                    discountTypesByShopId.TryGetValue(normalizedShopId, out discountType);

                var product = ShopProductFactory.CreateDailyProduct(row, discountType);
                if (product != null)
                    products.Add(product);
            }

            return products;
        }

        static List<SHOP_DAILY> selectDailyRows(IReadOnlyList<SHOP_DAILY> rows, int targetCount)
        {
            var mandatoryRows = new List<SHOP_DAILY>();
            var weightedRows = new List<SHOP_DAILY>();
            var seenShopIds = new HashSet<string>(StringComparer.Ordinal);

            if (rows == null || rows.Count <= 0)
                return mandatoryRows;

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null || string.IsNullOrWhiteSpace(row.ShopId))
                    continue;

                var normalizedShopId = row.ShopId.Trim();
                if (!seenShopIds.Add(normalizedShopId))
                    continue;

                var selectRate = sanitizeDailySelectRate(row.SelectRate);
                if (selectRate < 0f)
                {
                    mandatoryRows.Add(row);
                    continue;
                }

                if (selectRate > 0f)
                    weightedRows.Add(row);
            }

            var selectedRows = new List<SHOP_DAILY>(Math.Max(targetCount, mandatoryRows.Count));
            selectedRows.AddRange(mandatoryRows);
            if (targetCount <= 0 || selectedRows.Count >= targetCount || weightedRows.Count <= 0)
                return selectedRows;

            var remainingCount = targetCount - selectedRows.Count;
            for (var i = 0; i < remainingCount && weightedRows.Count > 0; i++)
            {
                if (!trySelectDailyRow(weightedRows, out var selectedRow) || selectedRow == null)
                    break;

                selectedRows.Add(selectedRow);
                weightedRows.Remove(selectedRow);
            }

            return selectedRows;
        }

        static bool trySelectDailyRow(IReadOnlyList<SHOP_DAILY> rows, out SHOP_DAILY selectedRow)
        {
            selectedRow = null;

            var totalRate = 0f;
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (!isSelectableDailyRow(row))
                    continue;

                totalRate += row.SelectRate;
            }

            if (!(totalRate > 0f))
                return false;

            var roll = UnityEngine.Random.value * totalRate;
            var cumulative = 0f;
            SHOP_DAILY lastRow = null;
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (!isSelectableDailyRow(row))
                    continue;

                cumulative += row.SelectRate;
                lastRow = row;
                if (roll < cumulative)
                {
                    selectedRow = row;
                    return true;
                }
            }

            if (lastRow == null)
                return false;

            selectedRow = lastRow;
            return true;
        }

        static bool isSelectableDailyRow(SHOP_DAILY row)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.ShopId))
                return false;

            return sanitizeDailySelectRate(row.SelectRate) > 0f;
        }

        static float sanitizeDailySelectRate(float selectRate)
        {
            if (float.IsNaN(selectRate) || float.IsInfinity(selectRate))
                return 0f;

            return selectRate;
        }

        static Dictionary<string, SHOP_DISCOUNT_TYPE> selectDailyDiscountTypes(
            IReadOnlyList<SHOP_DAILY> selectedRows,
            int targetDiscountCount)
        {
            var discountTypesByShopId = new Dictionary<string, SHOP_DISCOUNT_TYPE>(StringComparer.Ordinal);
            if (selectedRows == null || selectedRows.Count <= 0 || targetDiscountCount <= 0)
                return discountTypesByShopId;

            var candidates = new List<SHOP_DAILY>(selectedRows.Count);
            var seenShopIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < selectedRows.Count; i++)
            {
                var row = selectedRows[i];
                var normalizedShopId = NormalizeShopId(row?.ShopId);
                if (string.IsNullOrEmpty(normalizedShopId))
                    continue;

                if (!seenShopIds.Add(normalizedShopId))
                    continue;

                if (!hasSelectableDiscountRate(row))
                    continue;

                candidates.Add(row);
            }

            var discountSelectionCount = Math.Min(targetDiscountCount, candidates.Count);
            for (var i = 0; i < discountSelectionCount && candidates.Count > 0; i++)
            {
                var index = UnityEngine.Random.Range(0, candidates.Count);
                var row = candidates[index];
                candidates.RemoveAt(index);

                var normalizedShopId = NormalizeShopId(row?.ShopId);
                if (string.IsNullOrEmpty(normalizedShopId))
                    continue;

                discountTypesByShopId[normalizedShopId] = selectDailyDiscountType(row);
            }

            return discountTypesByShopId;
        }

        static SHOP_DISCOUNT_TYPE selectDailyDiscountType(SHOP_DAILY row)
        {
            if (row == null)
                return SHOP_DISCOUNT_TYPE.NONE;

            var rate10 = sanitizeDiscountRate(row.DiscountRate10Per);
            var rate20 = sanitizeDiscountRate(row.DiscountRate20Per);
            var rate30 = sanitizeDiscountRate(row.DiscountRate30Per);
            var rate50 = sanitizeDiscountRate(row.DiscountRate50Per);
            var totalRate = rate10 + rate20 + rate30 + rate50;
            if (!(totalRate > 0f))
                return SHOP_DISCOUNT_TYPE.NONE;

            var roll = UnityEngine.Random.value * totalRate;
            var cumulative = rate10;
            if (roll < cumulative)
                return SHOP_DISCOUNT_TYPE.PER10;

            cumulative += rate20;
            if (roll < cumulative)
                return SHOP_DISCOUNT_TYPE.PER20;

            cumulative += rate30;
            if (roll < cumulative)
                return SHOP_DISCOUNT_TYPE.PER30;

            return rate50 > 0f ? SHOP_DISCOUNT_TYPE.PER50 : SHOP_DISCOUNT_TYPE.NONE;
        }

        static bool hasSelectableDiscountRate(SHOP_DAILY row)
        {
            if (row == null)
                return false;

            var totalRate =
                sanitizeDiscountRate(row.DiscountRate10Per) +
                sanitizeDiscountRate(row.DiscountRate20Per) +
                sanitizeDiscountRate(row.DiscountRate30Per) +
                sanitizeDiscountRate(row.DiscountRate50Per);
            return totalRate > 0f;
        }

        static float sanitizeDiscountRate(float rate)
        {
            if (float.IsNaN(rate) || float.IsInfinity(rate))
                return 0f;

            return rate > 0f ? rate : 0f;
        }

        static SHOP_DISCOUNT_TYPE normalizeDiscountType(SHOP_DISCOUNT_TYPE discountType)
        {
            switch (discountType)
            {
                case SHOP_DISCOUNT_TYPE.PER10:
                case SHOP_DISCOUNT_TYPE.PER20:
                case SHOP_DISCOUNT_TYPE.PER30:
                case SHOP_DISCOUNT_TYPE.PER50:
                    return discountType;
                default:
                    return SHOP_DISCOUNT_TYPE.NONE;
            }
        }
    }

    public sealed class ShopCatalogChest : ShopCatalogBase
    {
        readonly IReadOnlyList<ShopProductBase> _prebuiltProducts;

        public ShopCatalogChest()
            : this(products: null)
        {
        }

        internal ShopCatalogChest(IReadOnlyList<ShopProductBase> products)
            : base(SHOP_CATALOG_TYPE.CHEST)
        {
            _prebuiltProducts = products;
        }

        protected override IReadOnlyList<ShopProductBase> onInitialize()
        {
            if (_prebuiltProducts != null)
                return _prebuiltProducts;

            return BuildProductsFromRows(TB_SHOP_CHEST.GetAll(), ShopProductFactory.CreateChestProduct);
        }
    }

    public sealed class ShopCatalogPurchase : ShopCatalogBase
    {
        readonly IReadOnlyList<ShopProductBase> _prebuiltProducts;

        public ShopCatalogPurchase()
            : this(products: null)
        {
        }

        internal ShopCatalogPurchase(IReadOnlyList<ShopProductBase> products)
            : base(SHOP_CATALOG_TYPE.PURCHASE)
        {
            _prebuiltProducts = products;
        }

        protected override IReadOnlyList<ShopProductBase> onInitialize()
        {
            if (_prebuiltProducts != null)
                return _prebuiltProducts;

            return BuildProductsFromRows(TB_SHOP_PURCHASE.GetAll(), ShopProductFactory.CreatePurchaseProduct);
        }
    }

    public sealed class ShopCatalogGold : ShopCatalogBase
    {
        readonly IReadOnlyList<ShopProductBase> _prebuiltProducts;

        public ShopCatalogGold()
            : this(products: null)
        {
        }

        internal ShopCatalogGold(IReadOnlyList<ShopProductBase> products)
            : base(SHOP_CATALOG_TYPE.GOLD)
        {
            _prebuiltProducts = products;
        }

        protected override IReadOnlyList<ShopProductBase> onInitialize()
        {
            if (_prebuiltProducts != null)
                return _prebuiltProducts;

            return BuildProductsFromRows(TB_SHOP_GOLD.GetAll(), ShopProductFactory.CreateGoldProduct);
        }
    }

}
