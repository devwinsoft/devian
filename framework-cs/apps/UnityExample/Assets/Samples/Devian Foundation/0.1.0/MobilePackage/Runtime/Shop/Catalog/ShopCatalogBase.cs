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
        public string name_id => _catalogConfig.name_id ?? string.Empty;
        public virtual int autoRefreshDays => _catalogConfig.auto_refresh_days;
        public string unlock_msg_id => normalizeUnlockMsgId(_catalogConfig.unlock_msg_id);
        public GAME_MESSAGE_OP_TYPE unlock_op_type => normalizeUnlockOpType(_catalogConfig.unlock_op_type);
        public CBigInt unlock_value => normalizeUnlockValue(_catalogConfig.unlock_value);
        public bool IsLocked => _isLocked;
        public bool HasUnlockCondition => !string.IsNullOrWhiteSpace(unlock_msg_id);
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

        public GameResult ResetAds()
        {
            if (!ShopManager.TryGet(out var manager) || manager == null)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "ShopManager is unavailable.");
            }

            return manager.ResetAdsInternal(CatalogType);
        }

        public virtual Task<GameResult> RefreshByAdsAsync(CancellationToken ct = default)
        {
            return Task.FromResult(
                GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"RefreshByAdsAsync is not supported: catalog_type={CatalogType}"));
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
                SHOP_CATALOG_TYPE.CHEST => buildProductsFromRowsWithStorage(TB_SHOP_ITEM_CHEST.GetAll(), ShopProductFactory.CreateChestProduct),
                SHOP_CATALOG_TYPE.PURCHASE => buildProductsFromRowsWithStorage(TB_SHOP_ITEM_PURCHASE.GetAll(), ShopProductFactory.CreatePurchaseProduct),
                SHOP_CATALOG_TYPE.GOLD => buildProductsFromRowsWithStorage(TB_SHOP_ITEM_GOLD.GetAll(), ShopProductFactory.CreateGoldProduct),
                _ => EmptyProducts,
            };
        }

        internal virtual long GetNextProductRefreshUtcMs(long serverNowUtcMs)
        {
            return 0L;
        }

        internal virtual GameResult<bool> SyncRuntimeState(bool requireServerTime)
        {
            return GameResult<bool>.Success(false);
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
                    if (products[i] is not ShopLimitedProductBase limitedProduct
                        || !limitedProduct.HasPurchaseLimit)
                        continue;

                    if (!clearAdsFreeRemainState && IsLimitedAdsOrFreeProduct(limitedProduct))
                        continue;

                    var normalizedShopId = NormalizeShopId(limitedProduct.shop_id);
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
            return product is ShopLimitedProductBase limitedProduct
                && limitedProduct.HasPurchaseLimit
                && (product.ProductType == SHOP_PRODUCT_TYPE.ADS
                    || product.ProductType == SHOP_PRODUCT_TYPE.FREE);
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

            var normalizedShopId = NormalizeShopId(product.shop_id);
            if (string.IsNullOrEmpty(normalizedShopId))
                return;

            if (product is not ShopLimitedProductBase limitedProduct)
            {
                Storage.RemoveProductRemainCount(CatalogType, normalizedShopId);
                return;
            }

            if (!limitedProduct.HasPurchaseLimit)
            {
                Storage.RemoveProductRemainCount(CatalogType, normalizedShopId);
                return;
            }

            if (Storage.TryGetProductRemainCount(CatalogType, normalizedShopId, out var storedRemainCount))
            {
                limitedProduct.SetRemainCount(storedRemainCount);
            }
            else if (Storage.TryTakeLegacyPurchaseCount(normalizedShopId, out var legacyPurchaseCount))
            {
                limitedProduct.SetRemainCount(limitedProduct.max_count - legacyPurchaseCount);
            }
            else
            {
                limitedProduct.ResetRemainCount();
            }

            Storage.SetProductRemainCount(CatalogType, normalizedShopId, limitedProduct.RemainCount);
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

                var normalizedShopId = NormalizeShopId(product.shop_id);
                if (string.IsNullOrEmpty(normalizedShopId))
                    continue;

                if (_productsByShopId.ContainsKey(normalizedShopId))
                {
                    Debug.LogWarning(
                        $"[ShopCatalogBase] Duplicate shop_id in catalog. Keeping first row: catalog={CatalogType}, shop_id={normalizedShopId}");
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

            return !string.IsNullOrWhiteSpace(normalizeUnlockMsgId(catalogConfig.unlock_msg_id));
        }

        static SHOP_CATALOG normalizeCatalogConfig(SHOP_CATALOG_TYPE catalogType, SHOP_CATALOG catalogConfig)
        {
            var sourceConfig = catalogConfig ?? TB_SHOP_CATALOG.Get(catalogType);
            var normalizedCatalogType = catalogType != SHOP_CATALOG_TYPE.NONE
                ? catalogType
                : sourceConfig != null ? sourceConfig.catalog_type : SHOP_CATALOG_TYPE.NONE;

            return new SHOP_CATALOG
            {
                catalog_type = normalizedCatalogType,
                name_id = sourceConfig != null ? sourceConfig.name_id ?? string.Empty : string.Empty,
                auto_refresh_days = normalizeAutoRefreshDays(sourceConfig != null ? sourceConfig.auto_refresh_days : 0),
                unlock_msg_id = normalizeUnlockMsgId(sourceConfig != null ? sourceConfig.unlock_msg_id : string.Empty),
                unlock_op_type = normalizeUnlockOpType(sourceConfig != null ? sourceConfig.unlock_op_type : GAME_MESSAGE_OP_TYPE.GTE),
                unlock_value = normalizeUnlockValue(sourceConfig != null ? sourceConfig.unlock_value : CBigInt.Zero),
            };
        }

    }

}
