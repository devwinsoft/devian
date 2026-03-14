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
        protected readonly ShopStorage Storage;
        protected readonly ShopCatalogStorageDataBase StorageData;

        protected ShopCatalogBase(
            SHOP_CATALOG_TYPE catalogType,
            ShopStorage storage = null,
            ShopCatalogStorageDataBase storageData = null,
            SHOP_CATALOG catalogConfig = null,
            IReadOnlyList<ShopProductBase> prebuiltProducts = null)
        {
            CatalogType = catalogType;
            Storage = storage;
            StorageData = storageData;
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

            return manager.ResetAdsInternal(CatalogType);
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
                SHOP_CATALOG_TYPE.CHEST => buildProductsFromRowsWithStorage(TB_SHOP_CHEST.GetAll(), ShopProductFactory.CreateChestProduct),
                SHOP_CATALOG_TYPE.PURCHASE => buildProductsFromRowsWithStorage(TB_SHOP_PURCHASE.GetAll(), ShopProductFactory.CreatePurchaseProduct),
                SHOP_CATALOG_TYPE.GOLD => buildProductsFromRowsWithStorage(TB_SHOP_GOLD.GetAll(), ShopProductFactory.CreateGoldProduct),
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

        protected IReadOnlyList<ShopProductBase> buildProductsFromRowsWithStorage<TRow>(
            IReadOnlyList<TRow> rows,
            Func<TRow, ShopProductBase> createProduct)
        {
            var products = ShopProductFactory.BuildProductsFromRows(rows, createProduct);
            ApplyStoredProductState(products);
            return products;
        }

        protected void ApplyStoredProductState(IReadOnlyList<ShopProductBase> products)
        {
            if (Storage == null || products == null || products.Count <= 0)
                return;

            for (var i = 0; i < products.Count; i++)
            {
                ApplyStoredProductState(products[i]);
            }
        }

        protected void ApplyStoredProductState(ShopProductBase product)
        {
            if (Storage == null || product == null)
                return;

            var normalizedShopId = NormalizeShopId(product.ShopId);
            if (string.IsNullOrEmpty(normalizedShopId))
                return;

            if (!product.HasPurchaseLimit)
            {
                product.SetRemainCount(-1);
                Storage.RemoveProductRemainCount(CatalogType, normalizedShopId);
                return;
            }

            if (Storage.TryGetProductRemainCount(CatalogType, normalizedShopId, out var storedRemainCount))
            {
                product.SetRemainCount(storedRemainCount);
            }
            else if (Storage.TryTakeLegacyPurchaseCount(normalizedShopId, out var legacyPurchaseCount))
            {
                product.SetRemainCount(product.MaxCount - legacyPurchaseCount);
            }
            else
            {
                product.ResetRemainCount();
            }

            Storage.SetProductRemainCount(CatalogType, normalizedShopId, product.RemainCount);
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

    }

}
