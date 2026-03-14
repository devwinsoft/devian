using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Common;
using Devian.Domain.Game;
using UnityEngine;

namespace Devian
{
    public abstract class ShopCatalogBase
    {
        protected const long MillisecondsPerDay = 24L * 60L * 60L * 1000L;
        static readonly IReadOnlyList<ShopProductBase> EmptyProducts = Array.Empty<ShopProductBase>();

        IReadOnlyList<ShopProductBase> _products = EmptyProducts;
        readonly Dictionary<string, ShopProductBase> _productsByShopId = new(StringComparer.Ordinal);
        readonly SHOP_CATALOG _catalogConfig;
        readonly IReadOnlyList<ShopProductBase> _prebuiltProducts;
        bool _initialized;
        bool _isLocked;
        protected long remainAutoRefreshTimeMs;
        protected long remainAdsRefreshTimeMs;
        protected readonly ShopStorage Storage;

        protected ShopCatalogBase(
            SHOP_CATALOG_TYPE catalogType,
            ShopStorage storage = null,
            SHOP_CATALOG catalogConfig = null,
            IReadOnlyList<ShopProductBase> prebuiltProducts = null)
        {
            CatalogType = catalogType;
            Storage = storage;
            _catalogConfig = normalizeCatalogConfig(catalogType, catalogConfig);
            _prebuiltProducts = prebuiltProducts;
            _isLocked = hasUnlockCondition(_catalogConfig);
        }

        public SHOP_CATALOG_TYPE CatalogType { get; }
        public string NameId => _catalogConfig.NameId ?? string.Empty;
        public virtual int autoRefreshDays => _catalogConfig.AutoRefreshDays;
        public string UnlockMsgId => normalizeUnlockMsgId(_catalogConfig.UnlockMsgId);
        public GAME_MESSAGE_OP_TYPE UnlockOpType => normalizeUnlockOpType(_catalogConfig.UnlockOpType);
        public CBigInt UnlockValue => normalizeUnlockValue(_catalogConfig.UnlockValue);
        public bool IsLocked => _isLocked;
        public bool HasUnlockCondition => !string.IsNullOrWhiteSpace(UnlockMsgId);
        public long RemainAutoRefreshTimeMs => remainAutoRefreshTimeMs > 0L ? remainAutoRefreshTimeMs : 0L;
        public long RemainAdsRefreshTimeMs => remainAdsRefreshTimeMs > 0L ? remainAdsRefreshTimeMs : 0L;
        public long RemainRefreshTimeMs => RemainAutoRefreshTimeMs;
        internal SHOP_CATALOG CatalogConfig => _catalogConfig;
        protected IReadOnlyList<ShopProductBase> PrebuiltProducts => _prebuiltProducts;
        protected bool IsInitialized => _initialized;

        public void Initialize()
        {
            if (_initialized)
                return;

            onInitialize();
            _initialized = true;
            RefreshProducts();
        }

        public void RefreshProducts()
        {
            if (!_initialized)
                return;

            setProducts(onRefresh());
        }

        public CommonResult ResetAds()
        {
            if (!ShopManager.TryGet(out var manager) || manager == null)
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.SAVEDATA_SYNC_REQUIRED,
                    "ShopManager is unavailable.");
            }

            return manager.ResetAdsInternal(this);
        }

        public virtual Task<CommonResult> RefreshByAdsAsync(CancellationToken ct = default)
        {
            return Task.FromResult(
                CommonResult.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    $"RefreshByAdsAsync is not supported: catalogType={CatalogType}"));
        }

        protected virtual void onInitialize()
        {
        }

        protected virtual IReadOnlyList<ShopProductBase> onRefresh()
        {
            if (_prebuiltProducts != null)
                return _prebuiltProducts;

            return CatalogType switch
            {
                SHOP_CATALOG_TYPE.CHEST => BuildProductsFromRows(TB_SHOP_CHEST.GetAll(), CreateChestProduct),
                SHOP_CATALOG_TYPE.PURCHASE => BuildProductsFromRows(TB_SHOP_PURCHASE.GetAll(), CreatePurchaseProduct),
                SHOP_CATALOG_TYPE.GOLD => BuildProductsFromRows(TB_SHOP_GOLD.GetAll(), CreateGoldProduct),
                _ => EmptyProducts,
            };
        }

        internal virtual long GetNextProductRefreshUtcMs(long serverNowUtcMs)
        {
            return 0L;
        }

        internal virtual CommonResult<bool> SyncRuntimeState(bool requireServerTime)
        {
            return CommonResult<bool>.Success(false);
        }

        public IReadOnlyList<ShopProductBase> GetProducts()
        {
            return _products;
        }

        public ShopProductBase GetProduct(string shopId)
        {
            var normalizedShopId = NormalizeShopId(shopId);
            if (string.IsNullOrEmpty(normalizedShopId))
                return null;

            return _productsByShopId.TryGetValue(normalizedShopId, out var product)
                ? product
                : null;
        }

        internal void SetRemainAutoRefreshTimeMs(long remainTimeMs)
        {
            remainAutoRefreshTimeMs = remainTimeMs > 0L ? remainTimeMs : 0L;
        }

        internal void SetRemainAdsRefreshTimeMs(long remainTimeMs)
        {
            remainAdsRefreshTimeMs = remainTimeMs > 0L ? remainTimeMs : 0L;
        }

        internal void SetRemainRefreshTimeMs(long remainTimeMs)
        {
            SetRemainAutoRefreshTimeMs(remainTimeMs);
        }

        internal void SetLocked(bool isLocked)
        {
            _isLocked = HasUnlockCondition && isLocked;
        }

        internal void ClearRuntimeStateForRefresh(bool clearAdsFreeRemainState)
        {
            if (Storage == null)
                return;

            var products = GetProducts();
            if (products != null && products.Count > 0)
            {
                var limitedShopIds = new List<string>(products.Count);
                for (var i = 0; i < products.Count; i++)
                {
                    var product = products[i];
                    if (product == null || !product.HasPurchaseLimit)
                        continue;

                    if (!clearAdsFreeRemainState && IsLimitedAdsOrFreeProduct(product))
                        continue;

                    var normalizedShopId = NormalizeShopId(product.ShopId);
                    if (string.IsNullOrEmpty(normalizedShopId))
                        continue;

                    limitedShopIds.Add(normalizedShopId);
                }

                if (limitedShopIds.Count > 0)
                    Storage.ClearProductRemainCounts(CatalogType, limitedShopIds);
            }

            if (CatalogType == SHOP_CATALOG_TYPE.DAILY)
                Storage.ClearDailyCatalogProducts();
        }

        protected void ApplyNextAutoRefreshUtcMs(long serverNowUtcMs)
        {
            if (Storage == null || serverNowUtcMs <= 0L)
            {
                SetRemainAutoRefreshTimeMs(0L);
                return;
            }

            var refreshIntervalMs = GetAutoRefreshIntervalMs(autoRefreshDays);
            if (refreshIntervalMs <= 0L)
            {
                Storage.ClearAutoRefreshUtcMs(CatalogType);
                SetRemainAutoRefreshTimeMs(0L);
                return;
            }

            var nextAutoRefreshUtcMs = GetNextRefreshUtcMs(serverNowUtcMs, refreshIntervalMs);
            Storage.SetAutoRefreshUtcMs(CatalogType, nextAutoRefreshUtcMs);
            SetRemainAutoRefreshTimeMs(
                GetRemainingToNextRefreshMs(serverNowUtcMs, nextAutoRefreshUtcMs));
        }

        protected static string NormalizeShopId(string shopId)
        {
            return shopId != null ? shopId.Trim() : string.Empty;
        }

        protected static long GetAutoRefreshIntervalMs(int autoRefreshDays)
        {
            return autoRefreshDays > 0
                ? autoRefreshDays * MillisecondsPerDay
                : 0L;
        }

        protected static long GetRemainingToNextRefreshMs(long serverNowUtcMs, long nextRefreshUtcMs)
        {
            if (serverNowUtcMs <= 0L || nextRefreshUtcMs <= 0L)
                return 0L;

            if (serverNowUtcMs >= nextRefreshUtcMs)
                return 0L;

            return nextRefreshUtcMs - serverNowUtcMs;
        }

        protected static long GetNextRefreshUtcMs(long serverNowUtcMs, long refreshIntervalMs)
        {
            if (serverNowUtcMs <= 0L || refreshIntervalMs <= 0L)
                return 0L;

            if (long.MaxValue - serverNowUtcMs < refreshIntervalMs)
                return long.MaxValue;

            return serverNowUtcMs + refreshIntervalMs;
        }

        protected static bool IsLimitedAdsOrFreeProduct(ShopProductBase product)
        {
            return product is ShopRewardProductBase rewardProduct
                && rewardProduct.HasPurchaseLimit
                && (rewardProduct.ProductType == SHOP_PRODUCT_TYPE.ADS
                    || rewardProduct.ProductType == SHOP_PRODUCT_TYPE.FREE);
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

        protected static ShopProductBase CreateDailyProduct(
            SHOP_DAILY row,
            SHOP_DISCOUNT_TYPE discountType)
        {
            if (row == null)
                return null;

            return createRewardProduct(
                row.ShopId,
                row.NameId,
                SHOP_CATALOG_TYPE.DAILY,
                row.CurrencyType,
                row.Price,
                row.RewardGroupId,
                row.Amount,
                row.MaxCount,
                discountType);
        }

        protected static ShopProductBase CreateChestProduct(SHOP_CHEST row)
        {
            if (row == null)
                return null;

            return createRewardProduct(
                row.ShopId,
                row.NameId,
                SHOP_CATALOG_TYPE.CHEST,
                row.CurrencyType,
                row.Price,
                row.RewardGroupId,
                row.Amount,
                row.MaxCount,
                SHOP_DISCOUNT_TYPE.NONE);
        }

        protected static ShopProductBase CreateEventProduct(SHOP_EVENT row)
        {
            if (row == null)
                return null;

            return createRewardProduct(
                row.ShopId,
                row.NameId,
                SHOP_CATALOG_TYPE.EVENT,
                row.CurrencyType,
                row.Price,
                row.RewardGroupId,
                1,
                -1,
                SHOP_DISCOUNT_TYPE.NONE);
        }

        protected static ShopProductBase CreatePurchaseProduct(SHOP_PURCHASE row)
        {
            if (row == null)
                return null;

            return new ShopProductPurchase(
                row.ShopId,
                row.NameId,
                SHOP_CATALOG_TYPE.PURCHASE,
                row.InternalProductId,
                row.SeasonId,
                -1);
        }

        protected static ShopProductBase CreateGoldProduct(SHOP_GOLD row)
        {
            if (row == null)
                return null;

            return createRewardProduct(
                row.ShopId,
                row.NameId,
                SHOP_CATALOG_TYPE.GOLD,
                row.CurrencyType,
                row.Price,
                row.RewardGroupId,
                1,
                row.MaxCount,
                SHOP_DISCOUNT_TYPE.NONE);
        }

        public static IReadOnlyList<ShopCatalogBase> CreateDefaultCatalogs(ShopStorage storage)
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
                        $"[ShopCatalogBase] Duplicate SHOP_CATALOG row. Keeping first row: catalog={row.CatalogType}");
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
            if (products == null)
            {
                return catalogType switch
                {
                    SHOP_CATALOG_TYPE.DAILY => new ShopCatalogDaily(storage, catalogConfig),
                    SHOP_CATALOG_TYPE.EVENT => new ShopCatalogEvent(storage, catalogConfig),
                    SHOP_CATALOG_TYPE.CHEST => new ShopCatalogChest(storage, catalogConfig),
                    SHOP_CATALOG_TYPE.PURCHASE => new ShopCatalogPurchase(storage, catalogConfig),
                    SHOP_CATALOG_TYPE.GOLD => new ShopCatalogGold(storage, catalogConfig),
                    _ => new ShopCatalogEmpty(catalogType, storage, catalogConfig),
                };
            }

            return catalogType switch
            {
                SHOP_CATALOG_TYPE.DAILY => new ShopCatalogDaily(storage, products, catalogConfig),
                SHOP_CATALOG_TYPE.EVENT => new ShopCatalogEvent(storage, products, catalogConfig),
                SHOP_CATALOG_TYPE.CHEST => new ShopCatalogChest(storage, products, catalogConfig),
                SHOP_CATALOG_TYPE.PURCHASE => new ShopCatalogPurchase(storage, products, catalogConfig),
                SHOP_CATALOG_TYPE.GOLD => new ShopCatalogGold(storage, products, catalogConfig),
                _ => new ShopCatalogEmpty(catalogType, storage, catalogConfig),
            };
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

        static int normalizeAutoRefreshDays(int autoRefreshDays)
        {
            return autoRefreshDays > 0 ? autoRefreshDays : 0;
        }

        static string normalizeUnlockMsgId(string unlockMsgId)
        {
            return unlockMsgId != null ? unlockMsgId.Trim() : string.Empty;
        }

        static GAME_MESSAGE_OP_TYPE normalizeUnlockOpType(GAME_MESSAGE_OP_TYPE unlockOpType)
        {
            return unlockOpType switch
            {
                GAME_MESSAGE_OP_TYPE.EQ => GAME_MESSAGE_OP_TYPE.EQ,
                GAME_MESSAGE_OP_TYPE.LTE => GAME_MESSAGE_OP_TYPE.LTE,
                GAME_MESSAGE_OP_TYPE.GTE => GAME_MESSAGE_OP_TYPE.GTE,
                _ => GAME_MESSAGE_OP_TYPE.GTE,
            };
        }

        static CBigInt normalizeUnlockValue(CBigInt? unlockValue)
        {
            return unlockValue ?? CBigInt.Zero;
        }

        static bool hasUnlockCondition(SHOP_CATALOG catalogConfig)
        {
            if (catalogConfig == null)
                return false;

            return !string.IsNullOrWhiteSpace(normalizeUnlockMsgId(catalogConfig.UnlockMsgId));
        }

        static SHOP_CATALOG normalizeCatalogConfig(SHOP_CATALOG_TYPE catalogType, SHOP_CATALOG catalogConfig)
        {
            var sourceConfig = catalogConfig ?? TB_SHOP_CATALOG.Get(catalogType);
            var normalizedCatalogType = catalogType != SHOP_CATALOG_TYPE.NONE
                ? catalogType
                : sourceConfig != null ? sourceConfig.CatalogType : SHOP_CATALOG_TYPE.NONE;

            return new SHOP_CATALOG
            {
                CatalogType = normalizedCatalogType,
                NameId = sourceConfig != null ? sourceConfig.NameId ?? string.Empty : string.Empty,
                AutoRefreshDays = normalizeAutoRefreshDays(sourceConfig != null ? sourceConfig.AutoRefreshDays : 0),
                UnlockMsgId = normalizeUnlockMsgId(sourceConfig != null ? sourceConfig.UnlockMsgId : string.Empty),
                UnlockOpType = normalizeUnlockOpType(sourceConfig != null ? sourceConfig.UnlockOpType : GAME_MESSAGE_OP_TYPE.GTE),
                UnlockValue = normalizeUnlockValue(sourceConfig != null ? sourceConfig.UnlockValue : CBigInt.Zero),
            };
        }

        static ShopProductBase createRewardProduct(
            string shopId,
            string nameId,
            SHOP_CATALOG_TYPE catalogType,
            CURRENCY_TYPE currencyType,
            int price,
            string rewardGroupId,
            int amount,
            int maxCount,
            SHOP_DISCOUNT_TYPE discountType)
        {
            switch (currencyType)
            {
                case CURRENCY_TYPE.FREE:
                    return new ShopProductFree(shopId, nameId, catalogType, price, rewardGroupId, amount, maxCount, discountType);
                case CURRENCY_TYPE.ADS:
                    return new ShopProductAds(shopId, nameId, catalogType, price, rewardGroupId, amount, maxCount, discountType);
                default:
                    return new ShopProductCurrency(
                        shopId,
                        nameId,
                        catalogType,
                        currencyType,
                        price,
                        rewardGroupId,
                        amount,
                        maxCount,
                        discountType);
            }
        }
    }

}
