using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Common;
using Devian.Domain.Game;
using UnityEngine;

namespace Devian
{
    public sealed class ShopManager : CompoSingleton<ShopManager>
    {
        const string Tag = nameof(ShopManager);
        const int CatalogUnlockOwnerKey = 0x53484F50; // "SHOP"
        const long MillisecondsPerDay = 24L * 60L * 60L * 1000L;

        public const string DefaultRentalNoAdsId = "NO_ADS";

        readonly ShopStorage _storage = new();
        readonly Dictionary<SHOP_CATALOG_TYPE, ShopCatalogBase> _catalogs = new();
        readonly List<ShopCatalogBase> _catalogList = new();
        readonly Dictionary<string, ShopProductBase> _productsByShopItemId = new(StringComparer.Ordinal);
        readonly Dictionary<SHOP_CATALOG_TYPE, List<string>> _limitedShopItemIdsByCatalog = new();
        readonly object _runtimeSaveLock = new();

        bool _catalogInitialized;
        bool _initialized;
        bool _isCatalogUnlockSubscribed;
        bool _runtimeSavePending;
        bool _runtimeSaveInFlight;

        public ShopStorage Storage => _storage;
        public COMMON_ERROR_TYPE LastCanBuyErrorCode { get; private set; } = COMMON_ERROR_TYPE.SUCCESS;
        public string LastCanBuyErrorMessage { get; private set; } = "Success";
        public COMMON_ERROR_TYPE LastCanBuyInnerErrorCode { get; private set; } = COMMON_ERROR_TYPE.SUCCESS;
        public string LastCanBuyInnerErrorMessage { get; private set; } = "Success";

        protected override void onDestroy()
        {
            unSubscribeCatalogUnlockMessages();
        }

        public CommonResult Initialize()
        {
            return initializeCore(requireServerTime: true);
        }

        public CommonResult RefreshProducts(bool requireServerTime = true)
        {
            if (!_initialized || !_catalogInitialized || _catalogList.Count <= 0)
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.SAVEDATA_SYNC_REQUIRED,
                    "ShopManager.Initialize must be called before RefreshProducts.");
            }

            return refreshProductsCore(requireServerTime, refreshLockState: true);
        }

        public IReadOnlyList<ShopCatalogBase> GetCatalogs()
        {
            return _catalogList;
        }

        public ShopCatalogBase GetCatalog(SHOP_CATALOG_TYPE catalogType)
        {
            if (_catalogs.TryGetValue(catalogType, out var catalog))
                return catalog;

            return ShopCatalogFactory.Empty(catalogType);
        }

        public T GetCatalog<T>() where T : ShopCatalogBase
        {
            for (var i = 0; i < _catalogList.Count; i++)
            {
                if (_catalogList[i] is T typedCatalog)
                    return typedCatalog;
            }

            return null;
        }

        internal void InvalidateRuntimeState()
        {
            unSubscribeCatalogUnlockMessages();
            _initialized = false;
            _catalogInitialized = false;
            _catalogs.Clear();
            _catalogList.Clear();
            _productsByShopItemId.Clear();
            _limitedShopItemIdsByCatalog.Clear();
        }

        public bool CanBuy(string shopItemId)
        {
            var check = checkCanBuy(shopItemId);
            if (check.IsFailure)
            {
                var inner = check.Error!;
                LastCanBuyErrorCode = COMMON_ERROR_TYPE.SHOP_CAN_BUY_FAILED;
                LastCanBuyErrorMessage =
                    $"Shop CanBuy failed: shop_item_id={normalizeShopItemId(shopItemId)}, inner={inner.Code}:{inner.Message}";
                LastCanBuyInnerErrorCode = inner.Code;
                LastCanBuyInnerErrorMessage = inner.Message;
                return false;
            }

            LastCanBuyErrorCode = COMMON_ERROR_TYPE.SUCCESS;
            LastCanBuyErrorMessage = "Success";
            LastCanBuyInnerErrorCode = COMMON_ERROR_TYPE.SUCCESS;
            LastCanBuyInnerErrorMessage = "Success";
            return true;
        }

        public async Task<CommonResult<RewardData[]>> BuyAsync(string shopItemId, CancellationToken ct = default)
        {
            var check = checkCanBuy(shopItemId);
            if (check.IsFailure)
                return wrapBuyFailure(shopItemId, check.Error!);

            var product = check.Value!;
            CommonResult<RewardData[]> buyResult;
            if (product is ShopProductPurchase purchaseProduct)
                buyResult = await buyPurchaseCatalogAsync(purchaseProduct, ct);
            else if (product is ShopProductChest chestProduct)
                buyResult = await buyChestCatalogAsync(chestProduct, ct);
            else if (product is ShopRewardProductBase rewardProduct)
                buyResult = await buyRewardCatalogAsync(rewardProduct, ct);
            else
                buyResult = CommonResult<RewardData[]>.Failure(
                    COMMON_ERROR_TYPE.SHOP_PRODUCT_NOT_FOUND,
                    $"Shop product type is not supported: shop_item_id={product.shop_item_id}, productType={product.ProductType}");

            if (buyResult.IsFailure)
                return wrapBuyFailure(shopItemId, buyResult.Error!);

            return buyResult;
        }

        readonly struct CatalogRefreshState
        {
            public CatalogRefreshState(
                long serverNowUtcMs,
                long autoRefreshIntervalMs,
                long nextCatalogRefreshUtcMs,
                long remainAutoRefreshTimeMs,
                long remainAdsRefreshTimeMs,
                bool hasLimitedAdsOrFreeProducts,
                bool shouldRefreshCatalogProducts,
                bool shouldRefillAdsFreeProducts,
                bool shouldInitializeAutoRefreshUtcMs,
                bool shouldClearAutoRefreshUtcMs,
                bool shouldClearAdsRefreshUtcMs)
            {
                ServerNowUtcMs = serverNowUtcMs;
                AutoRefreshIntervalMs = autoRefreshIntervalMs;
                NextCatalogRefreshUtcMs = nextCatalogRefreshUtcMs > 0L ? nextCatalogRefreshUtcMs : 0L;
                RemainAutoRefreshTimeMs = remainAutoRefreshTimeMs > 0L ? remainAutoRefreshTimeMs : 0L;
                RemainAdsRefreshTimeMs = remainAdsRefreshTimeMs > 0L ? remainAdsRefreshTimeMs : 0L;
                HasLimitedAdsOrFreeProducts = hasLimitedAdsOrFreeProducts;
                ShouldRefreshCatalogProducts = shouldRefreshCatalogProducts;
                ShouldRefillAdsFreeProducts = shouldRefillAdsFreeProducts;
                ShouldInitializeAutoRefreshUtcMs = shouldInitializeAutoRefreshUtcMs;
                ShouldClearAutoRefreshUtcMs = shouldClearAutoRefreshUtcMs;
                ShouldClearAdsRefreshUtcMs = shouldClearAdsRefreshUtcMs;
            }

            public long ServerNowUtcMs { get; }
            public long AutoRefreshIntervalMs { get; }
            public long NextCatalogRefreshUtcMs { get; }
            public long RemainAutoRefreshTimeMs { get; }
            public long RemainAdsRefreshTimeMs { get; }
            public bool HasLimitedAdsOrFreeProducts { get; }
            public bool ShouldRefreshCatalogProducts { get; }
            public bool ShouldRefillAdsFreeProducts { get; }
            public bool ShouldInitializeAutoRefreshUtcMs { get; }
            public bool ShouldClearAutoRefreshUtcMs { get; }
            public bool ShouldClearAdsRefreshUtcMs { get; }
            public bool HasServerTime => ServerNowUtcMs > 0L;
        }

        readonly struct CatalogRefreshCycleOutcome
        {
            public CatalogRefreshCycleOutcome(bool didRefreshCatalogProducts, bool didMutateStorage)
            {
                DidRefreshCatalogProducts = didRefreshCatalogProducts;
                DidMutateStorage = didMutateStorage;
            }

            public bool DidRefreshCatalogProducts { get; }
            public bool DidMutateStorage { get; }
        }

        readonly struct ChestPurchaseState
        {
            public ChestPurchaseState(
                ShopCatalogChest catalog,
                ShopCatalogChest.ChestPurchaseRuntime runtime)
            {
                Catalog = catalog;
                Runtime = runtime;
            }

            public ShopCatalogChest Catalog { get; }
            public ShopCatalogChest.ChestPurchaseRuntime Runtime { get; }
        }

        CommonResult<ShopProductBase> checkCanBuy(string shopItemId)
        {
            var validateProduct = validateShopProductConfig(shopItemId);
            if (validateProduct.IsFailure)
                return CommonResult<ShopProductBase>.Failure(validateProduct.Error!);

            var product = validateProduct.Value!;

            if (product is ShopProductPurchase purchaseProduct)
            {
                var validateSeason = validateSeasonPurchaseWindow(purchaseProduct);
                if (validateSeason.IsFailure)
                    return CommonResult<ShopProductBase>.Failure(validateSeason.Error!);

                return CommonResult<ShopProductBase>.Success(product);
            }

            if (product is ShopProductChest chestProduct)
            {
                var resolveChest = resolveChestPurchaseRuntime(chestProduct);
                if (resolveChest.IsFailure)
                    return CommonResult<ShopProductBase>.Failure(resolveChest.Error!);

                return checkStandardShopPurchaseCanBuy(chestProduct, chestProduct.currency_type);
            }

            if (product is not ShopRewardProductBase rewardProduct)
            {
                return CommonResult<ShopProductBase>.Failure(
                    COMMON_ERROR_TYPE.SHOP_PRODUCT_NOT_FOUND,
                    $"Shop product type is not supported: shop_item_id={product.shop_item_id}, productType={product.ProductType}");
            }

            return checkStandardShopPurchaseCanBuy(rewardProduct, rewardProduct.currency_type);
        }

        internal CommonResult ResetAdsInternal(SHOP_CATALOG_TYPE catalogType)
        {
            if (!_initialized || !_catalogInitialized || _catalogList.Count <= 0)
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.SAVEDATA_SYNC_REQUIRED,
                    "ShopManager.Initialize must be called before ResetAds.");
            }

            if (!isValidCatalogType(catalogType))
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    $"Invalid shop catalog_type for ResetAds: {catalogType}");
            }

            var refresh = tryRefreshCatalog(
                catalogType,
                requireServerTime: true,
                forceCatalogRefresh: true);
            if (refresh.IsFailure)
                return CommonResult.Failure(refresh.Error!);

            if (refresh.Value.DidRefreshCatalogProducts)
                synchronizeProductIndexFromCatalogs();

            if (refresh.Value.DidMutateStorage || refresh.Value.DidRefreshCatalogProducts)
                queueRuntimeLocalSave();

            return CommonResult.Ok();
        }

        CommonResult<ShopProductBase> validateShopProductConfig(string shopItemId)
        {
            if (!_initialized || !_catalogInitialized || _catalogList.Count <= 0)
            {
                return CommonResult<ShopProductBase>.Failure(
                    COMMON_ERROR_TYPE.SAVEDATA_SYNC_REQUIRED,
                    "ShopManager.Initialize must be called before CanBuy/BuyAsync.");
            }

            var refresh = refreshProductsCore(requireServerTime: true, refreshLockState: false);
            if (refresh.IsFailure)
                return CommonResult<ShopProductBase>.Failure(refresh.Error!);

            var normalizedShopItemId = normalizeShopItemId(shopItemId);
            if (string.IsNullOrEmpty(normalizedShopItemId))
            {
                return CommonResult<ShopProductBase>.Failure(
                    COMMON_ERROR_TYPE.SHOP_PRODUCT_ID_EMPTY,
                    "Shop shop_item_id is empty.");
            }

            synchronizeProductIndexFromCatalogs();

            if (!_productsByShopItemId.TryGetValue(normalizedShopItemId, out var product) || product == null)
            {
                return CommonResult<ShopProductBase>.Failure(
                    COMMON_ERROR_TYPE.SHOP_PRODUCT_NOT_FOUND,
                    $"Shop product not found: shop_item_id={normalizedShopItemId}");
            }

            if (_catalogs.TryGetValue(product.catalog_type, out var productCatalog)
                && productCatalog != null
                && productCatalog.IsLocked)
            {
                return CommonResult<ShopProductBase>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    $"Shop catalog is locked: catalog_type={product.catalog_type}, shop_item_id={normalizedShopItemId}");
            }

            if (product is ShopRewardProductBase rewardProduct && rewardProduct.PriceWithoutDiscount < 0)
            {
                return CommonResult<ShopProductBase>.Failure(
                    COMMON_ERROR_TYPE.SHOP_PRODUCT_PRICE_INVALID,
                    $"Shop product price is invalid: shop_item_id={product.shop_item_id}, price={rewardProduct.PriceWithoutDiscount}, discountType={rewardProduct.DiscountType}");
            }

            if (product is ShopProductChest chestProduct && chestProduct.PriceWithoutDiscount < 0)
            {
                return CommonResult<ShopProductBase>.Failure(
                    COMMON_ERROR_TYPE.SHOP_PRODUCT_PRICE_INVALID,
                    $"Shop product price is invalid: shop_item_id={product.shop_item_id}, price={chestProduct.PriceWithoutDiscount}, discountType={chestProduct.DiscountType}");
            }

            if (product.HasPurchaseLimit)
            {
                if (product.max_count == 0)
                {
                    return CommonResult<ShopProductBase>.Failure(
                        COMMON_ERROR_TYPE.SHOP_ITEM_PURCHASE_LIMIT_DISABLED,
                        $"Shop purchase is disabled by max_count=0: shop_item_id={product.shop_item_id}");
                }

                if (product.RemainCount <= 0)
                {
                    var usedCount = product.max_count - product.RemainCount;
                    return CommonResult<ShopProductBase>.Failure(
                        COMMON_ERROR_TYPE.SHOP_ITEM_PURCHASE_LIMIT_EXCEEDED,
                        $"Shop purchase limit exceeded: shop_item_id={product.shop_item_id}, max_count={product.max_count}, remainCount={product.RemainCount}, usedCount={usedCount}");
                }
            }

            if (product is ShopProductPurchase purchaseProduct)
            {
                if (string.IsNullOrWhiteSpace(purchaseProduct.internal_product_id))
                {
                    return CommonResult<ShopProductBase>.Failure(
                        COMMON_ERROR_TYPE.PURCHASE_NOT_FOUND,
                        $"Shop purchase internal_product_id is empty: shop_item_id={purchaseProduct.shop_item_id}");
                }

                if (!tryResolvePurchaseProduct(purchaseProduct.internal_product_id, out var purchaseRow) || purchaseRow == null)
                {
                    return CommonResult<ShopProductBase>.Failure(
                        COMMON_ERROR_TYPE.PURCHASE_NOT_FOUND,
                        $"Purchase product not found: shop_item_id={purchaseProduct.shop_item_id}, internal_product_id={purchaseProduct.internal_product_id}");
                }

                if (!purchaseRow.is_active)
                {
                    return CommonResult<ShopProductBase>.Failure(
                        COMMON_ERROR_TYPE.PURCHASE_NOT_FOUND,
                        $"Purchase product is inactive: shop_item_id={purchaseProduct.shop_item_id}, internal_product_id={purchaseProduct.internal_product_id}");
                }

                return CommonResult<ShopProductBase>.Success(product);
            }

            if (product is ShopProductChest validateChestProduct)
            {
                if (validateChestProduct.ProductType == SHOP_PRODUCT_TYPE.ADS
                    && validateChestProduct.PriceWithoutDiscount != 0)
                {
                    return CommonResult<ShopProductBase>.Failure(
                        COMMON_ERROR_TYPE.SHOP_PRODUCT_PRICE_INVALID,
                        $"Shop pseudo product price must be zero: shop_item_id={validateChestProduct.shop_item_id}, productType={validateChestProduct.ProductType}, price={validateChestProduct.PriceWithoutDiscount}");
                }

                if (validateChestProduct.chest_type == SHOP_PRODUCT_CHEST_TYPE.NONE)
                {
                    return CommonResult<ShopProductBase>.Failure(
                        COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                        $"Shop chest_type is invalid: shop_item_id={validateChestProduct.shop_item_id}");
                }

                var resolveChest = resolveChestPurchaseRuntime(validateChestProduct);
                if (resolveChest.IsFailure)
                    return CommonResult<ShopProductBase>.Failure(resolveChest.Error!);

                return CommonResult<ShopProductBase>.Success(product);
            }

            if (product is not ShopRewardProductBase validateRewardProduct)
            {
                return CommonResult<ShopProductBase>.Failure(
                    COMMON_ERROR_TYPE.SHOP_PRODUCT_NOT_FOUND,
                    $"Shop product type is not supported: shop_item_id={product.shop_item_id}, productType={product.ProductType}");
            }

            if (validateRewardProduct.ProductType == SHOP_PRODUCT_TYPE.FREE
                || validateRewardProduct.ProductType == SHOP_PRODUCT_TYPE.ADS)
            {
                if (validateRewardProduct.PriceWithoutDiscount != 0)
                {
                    return CommonResult<ShopProductBase>.Failure(
                        COMMON_ERROR_TYPE.SHOP_PRODUCT_PRICE_INVALID,
                        $"Shop pseudo product price must be zero: shop_item_id={validateRewardProduct.shop_item_id}, productType={validateRewardProduct.ProductType}, price={validateRewardProduct.PriceWithoutDiscount}");
                }
            }

            if (string.IsNullOrWhiteSpace(validateRewardProduct.reward_group_id))
            {
                return CommonResult<ShopProductBase>.Failure(
                    COMMON_ERROR_TYPE.SHOP_REWARD_GROUP_EMPTY,
                    $"Shop product reward_group_id is empty: shop_item_id={product.shop_item_id}");
            }

            return CommonResult<ShopProductBase>.Success(product);
        }

        async Task<CommonResult<RewardData[]>> buyPurchaseCatalogAsync(ShopProductPurchase product, CancellationToken ct)
        {
            var validateSeason = validateSeasonPurchaseWindow(product);
            if (validateSeason.IsFailure)
                return CommonResult<RewardData[]>.Failure(validateSeason.Error!);

            var purchaseResult = await PurchaseManager.Instance.PurchaseAsync(product.internal_product_id, ct);
            if (purchaseResult.IsFailure)
                return CommonResult<RewardData[]>.Failure(purchaseResult.Error!);

            if (product.HasPurchaseLimit)
                markProductPurchased(product);

            var save = await SaveDataManager.Instance.SaveGameStorageAsync(true, ct);
            if (save.IsFailure)
                Debug.LogWarning($"[{Tag}] Post-shop purchase save failed (non-fatal): {save.Error}");

            var applied = purchaseResult.Value.AppliedRewards ?? Array.Empty<RewardData>();
            return CommonResult<RewardData[]>.Success(applied);
        }

        async Task<CommonResult<RewardData[]>> buyChestCatalogAsync(ShopProductChest product, CancellationToken ct)
        {
            var resolveChest = resolveChestPurchaseRuntime(product);
            if (resolveChest.IsFailure)
                return CommonResult<RewardData[]>.Failure(resolveChest.Error!);

            var chestState = resolveChest.Value;
            var wallet = default(InventoryWallet);
            var deduction = default(CurrencyDeduction);

            if (product.ProductType == SHOP_PRODUCT_TYPE.ADS)
            {
                var hasNoAdsRental = tryGetNoAdsRentalRemainingMs(out var noAdsRentalRemainingMs)
                    && noAdsRentalRemainingMs > 0L;

                if (!hasNoAdsRental)
                {
                    var show = await AdsManager.Instance.ShowAsync(ct);
                    if (show.IsFailure)
                    {
                        var details = show.Error != null
                            ? $"inner={show.Error.Code}:{show.Error.Message}"
                            : "inner=unknown";
                        return CommonResult<RewardData[]>.Failure(
                            COMMON_ERROR_TYPE.SHOP_ADS_SHOW_FAILED,
                            $"Shop rewarded ad show failed: shop_item_id={product.shop_item_id}, {details}");
                    }
                }
            }
            else if (product.ProductType == SHOP_PRODUCT_TYPE.CURRENCY)
            {
                if (!tryGetWallet(out wallet) || wallet == null)
                {
                    return CommonResult<RewardData[]>.Failure(
                        COMMON_ERROR_TYPE.SHOP_WALLET_UNAVAILABLE,
                        $"Inventory wallet is unavailable: shop_item_id={product.shop_item_id}");
                }

                var price = product.Price;
                if (price < 0)
                {
                    return CommonResult<RewardData[]>.Failure(
                        COMMON_ERROR_TYPE.SHOP_PRODUCT_PRICE_INVALID,
                        $"Shop product price is invalid: shop_item_id={product.shop_item_id}, price={price}, basePrice={product.PriceWithoutDiscount}, discountType={product.DiscountType}");
                }

                if (!tryDeductCurrency(wallet, product.currency_type, price, out deduction))
                {
                    return CommonResult<RewardData[]>.Failure(
                        COMMON_ERROR_TYPE.SHOP_CURRENCY_INSUFFICIENT,
                        $"Insufficient currency for shop purchase: shop_item_id={product.shop_item_id}, currency={product.currency_type}, price={price}, basePrice={product.PriceWithoutDiscount}, discountType={product.DiscountType}");
                }
            }
            else
            {
                return CommonResult<RewardData[]>.Failure(
                    COMMON_ERROR_TYPE.SHOP_PRODUCT_NOT_FOUND,
                    $"Shop chest product type is not supported: shop_item_id={product.shop_item_id}, productType={product.ProductType}, chest_type={product.chest_type}");
            }

            var runtime = chestState.Runtime;
            var applyRewards = applyShopProductRewards(runtime.RewardGroupId, runtime.RewardAmount);
            if (applyRewards.IsFailure)
            {
                if (deduction.HasDeduction)
                    rollbackCurrency(wallet, deduction);

                var details = applyRewards.Error != null
                    ? $"inner={applyRewards.Error.Code}:{applyRewards.Error.Message}"
                    : "inner=unknown";
                return CommonResult<RewardData[]>.Failure(
                    COMMON_ERROR_TYPE.SHOP_REWARD_APPLY_FAILED,
                    $"Shop chest reward apply failed: shop_item_id={product.shop_item_id}, reward_group_id={runtime.RewardGroupId}, {details}");
            }

            chestState.Catalog.AddExp(runtime.GainExp);

            if (product.HasPurchaseLimit)
                markProductPurchased(product);

            markAdsRefreshOnPurchaseIfNeeded(product);

            var save = await SaveDataManager.Instance.SaveGameStorageAsync(true, ct);
            if (save.IsFailure)
                Debug.LogWarning($"[{Tag}] Post-shop chest purchase save failed (non-fatal): {save.Error}");

            var applied = applyRewards.Value ?? Array.Empty<RewardData>();
            return CommonResult<RewardData[]>.Success(applied);
        }

        async Task<CommonResult<RewardData[]>> buyRewardCatalogAsync(ShopRewardProductBase product, CancellationToken ct)
        {
            var wallet = default(InventoryWallet);
            var deduction = default(CurrencyDeduction);

            if (product.ProductType == SHOP_PRODUCT_TYPE.ADS)
            {
                var hasNoAdsRental = tryGetNoAdsRentalRemainingMs(out var noAdsRentalRemainingMs)
                    && noAdsRentalRemainingMs > 0L;

                if (!hasNoAdsRental)
                {
                    var show = await AdsManager.Instance.ShowAsync(ct);
                    if (show.IsFailure)
                    {
                        var details = show.Error != null
                            ? $"inner={show.Error.Code}:{show.Error.Message}"
                            : "inner=unknown";
                        return CommonResult<RewardData[]>.Failure(
                            COMMON_ERROR_TYPE.SHOP_ADS_SHOW_FAILED,
                            $"Shop rewarded ad show failed: shop_item_id={product.shop_item_id}, {details}");
                    }
                }
            }
            else if (product.ProductType == SHOP_PRODUCT_TYPE.CURRENCY)
            {
                if (!tryGetWallet(out wallet) || wallet == null)
                {
                    return CommonResult<RewardData[]>.Failure(
                        COMMON_ERROR_TYPE.SHOP_WALLET_UNAVAILABLE,
                        $"Inventory wallet is unavailable: shop_item_id={product.shop_item_id}");
                }

                var price = product.Price;
                if (price < 0)
                {
                    return CommonResult<RewardData[]>.Failure(
                        COMMON_ERROR_TYPE.SHOP_PRODUCT_PRICE_INVALID,
                        $"Shop product price is invalid: shop_item_id={product.shop_item_id}, price={price}, basePrice={product.PriceWithoutDiscount}, discountType={product.DiscountType}");
                }

                if (!tryDeductCurrency(wallet, product.currency_type, price, out deduction))
                {
                    return CommonResult<RewardData[]>.Failure(
                        COMMON_ERROR_TYPE.SHOP_CURRENCY_INSUFFICIENT,
                        $"Insufficient currency for shop purchase: shop_item_id={product.shop_item_id}, currency={product.currency_type}, price={price}, basePrice={product.PriceWithoutDiscount}, discountType={product.DiscountType}");
                }
            }
            else if (product.ProductType != SHOP_PRODUCT_TYPE.FREE)
            {
                return CommonResult<RewardData[]>.Failure(
                    COMMON_ERROR_TYPE.SHOP_PRODUCT_NOT_FOUND,
                    $"Shop product type is not supported: shop_item_id={product.shop_item_id}, productType={product.ProductType}");
            }

            var applyRewards = applyShopProductRewards(product.reward_group_id, product.amount);
            if (applyRewards.IsFailure)
            {
                if (deduction.HasDeduction)
                    rollbackCurrency(wallet, deduction);

                var details = applyRewards.Error != null
                    ? $"inner={applyRewards.Error.Code}:{applyRewards.Error.Message}"
                    : "inner=unknown";
                return CommonResult<RewardData[]>.Failure(
                    COMMON_ERROR_TYPE.SHOP_REWARD_APPLY_FAILED,
                    $"Shop reward apply failed: shop_item_id={product.shop_item_id}, reward_group_id={product.reward_group_id}, {details}");
            }

            if (product.HasPurchaseLimit)
                markProductPurchased(product);

            markAdsRefreshOnPurchaseIfNeeded(product);

            var save = await SaveDataManager.Instance.SaveGameStorageAsync(true, ct);
            if (save.IsFailure)
                Debug.LogWarning($"[{Tag}] Post-shop purchase save failed (non-fatal): {save.Error}");

            var applied = applyRewards.Value ?? Array.Empty<RewardData>();
            return CommonResult<RewardData[]>.Success(applied);
        }

        CommonResult validateSeasonPurchaseWindow(ShopProductPurchase product)
        {
            if (!tryResolvePurchaseProduct(product.internal_product_id, out var purchaseProduct) || purchaseProduct == null)
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.PURCHASE_NOT_FOUND,
                    $"Purchase product not found: shop_item_id={product.shop_item_id}, internal_product_id={product.internal_product_id}");
            }

            var seasonId = (product.season_id ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(seasonId))
                return CommonResult.Ok();

            var season = TB_SEASON.Get(seasonId);
            if (season == null)
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    $"Season not found for shop purchase: shop_item_id={product.shop_item_id}, internal_product_id={product.internal_product_id}, season_id={seasonId}");
            }

            var seasonEndUtcMs = season.end_utc_time?.utcTimeMs ?? 0L;
            if (seasonEndUtcMs <= 0L)
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    $"Season end time is invalid: shop_item_id={product.shop_item_id}, internal_product_id={product.internal_product_id}, season_id={seasonId}");
            }

            var serverNowUtcMs = RemoteDataManager.ServerNowUtcMs;

            var blockedBeforeEndDays = 0;
            try
            {
                blockedBeforeEndDays = PurchaseManager.Instance.SeasonPurchaseBlockedBeforeEndDays;
            }
            catch
            {
                blockedBeforeEndDays = 0;
            }

            if (blockedBeforeEndDays < 0)
                blockedBeforeEndDays = 0;

            var seasonPurchaseBlockedBeforeEnd = TimeSpan.FromDays(blockedBeforeEndDays);
            var blockStartUtcMs = seasonEndUtcMs - (long)seasonPurchaseBlockedBeforeEnd.TotalMilliseconds;
            if (serverNowUtcMs >= blockStartUtcMs)
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.PURCHASE_SEASON_END_SOON_BLOCKED,
                    $"Product purchase is blocked near season end: blockDays={blockedBeforeEndDays}, shop_item_id={product.shop_item_id}, internal_product_id={product.internal_product_id}, season_id={seasonId}");
            }

            return CommonResult.Ok();
        }

        CommonResult<CatalogRefreshState> evaluateCatalogRefreshState(
            ShopCatalogBase catalog,
            bool requireServerTime,
            bool forceCatalogRefresh)
        {
            if (catalog == null || !isValidCatalogType(catalog.CatalogType))
            {
                return CommonResult<CatalogRefreshState>.Success(
                    new CatalogRefreshState(0L, 0L, 0L, 0L, 0L, false, false, false, false, false, false));
            }

            var intervalMs = getAutoRefreshIntervalMs(catalog.autoRefreshDays);
            var hasLimitedAdsOrFreeProductsInCatalog = hasLimitedAdsOrFreeProducts(catalog.GetProducts());

            var serverNowUtcMs = RemoteDataManager.ServerNowUtcMs;
            var requiresTimedServerRefresh = intervalMs > 0L || catalog is ShopCatalogEvent;
            if (serverNowUtcMs <= 0L
                && requireServerTime
                && (requiresTimedServerRefresh || hasLimitedAdsOrFreeProductsInCatalog))
            {
                return CommonResult<CatalogRefreshState>.Failure(
                    COMMON_ERROR_TYPE.SHOP_SERVER_TIME_UNAVAILABLE,
                    "Server time is invalid.");
            }

            var catalogType = catalog.CatalogType;
            var remainAutoRefreshTimeMs = 0L;
            var shouldRefreshCatalogProducts = forceCatalogRefresh;
            var shouldInitializeAutoRefreshUtcMs = false;
            var shouldClearAutoRefreshUtcMs = false;
            var nextCatalogRefreshUtcMs = 0L;
            var storedAutoRefreshUtcMs = _storage.GetAutoRefreshUtcMs(catalogType);

            if (catalog is ShopCatalogEvent)
            {
                if (serverNowUtcMs > 0L)
                {
                    nextCatalogRefreshUtcMs = catalog.GetNextProductRefreshUtcMs(serverNowUtcMs);
                    intervalMs = getRemainingToNextRefreshMs(serverNowUtcMs, nextCatalogRefreshUtcMs);
                    remainAutoRefreshTimeMs = intervalMs;

                    if (!shouldRefreshCatalogProducts
                        && storedAutoRefreshUtcMs > 0L
                        && storedAutoRefreshUtcMs <= serverNowUtcMs)
                    {
                        shouldRefreshCatalogProducts = true;
                    }

                    if (!shouldRefreshCatalogProducts)
                    {
                        if (nextCatalogRefreshUtcMs > 0L)
                        {
                            shouldInitializeAutoRefreshUtcMs = storedAutoRefreshUtcMs != nextCatalogRefreshUtcMs;
                        }
                        else
                        {
                            shouldClearAutoRefreshUtcMs = storedAutoRefreshUtcMs > 0L;
                        }
                    }
                }
                else
                {
                    remainAutoRefreshTimeMs = catalog.RemainAutoRefreshTimeMs;
                }
            }
            else if (intervalMs <= 0L)
            {
                shouldClearAutoRefreshUtcMs = storedAutoRefreshUtcMs > 0L;
            }
            else if (serverNowUtcMs > 0L)
            {
                var nextAutoRefreshUtcMs = storedAutoRefreshUtcMs;
                if (nextAutoRefreshUtcMs <= 0L)
                {
                    nextCatalogRefreshUtcMs = getNextRefreshUtcMs(serverNowUtcMs, intervalMs);
                    shouldInitializeAutoRefreshUtcMs = true;
                    remainAutoRefreshTimeMs = intervalMs;
                }
                else
                {
                    nextCatalogRefreshUtcMs = nextAutoRefreshUtcMs;
                    remainAutoRefreshTimeMs = getRemainingToNextRefreshMs(serverNowUtcMs, nextAutoRefreshUtcMs);
                    shouldRefreshCatalogProducts = remainAutoRefreshTimeMs <= 0L;
                }
            }
            else
            {
                remainAutoRefreshTimeMs = catalog.RemainAutoRefreshTimeMs;
            }

            var shouldRefillAdsFreeProducts = false;
            var shouldClearAdsRefreshUtcMs = false;
            var remainAdsRefreshTimeMs = 0L;
            if (!hasLimitedAdsOrFreeProductsInCatalog)
            {
                shouldClearAdsRefreshUtcMs = _storage.GetAdsRefreshUtcMs(catalogType) > 0L;
            }
            else if (serverNowUtcMs > 0L)
            {
                var nextAdsRefreshUtcMs = _storage.GetAdsRefreshUtcMs(catalogType);
                remainAdsRefreshTimeMs = getRemainingToNextRefreshMs(serverNowUtcMs, nextAdsRefreshUtcMs);
                shouldRefillAdsFreeProducts =
                    forceCatalogRefresh
                    || nextAdsRefreshUtcMs <= 0L
                    || nextAdsRefreshUtcMs <= serverNowUtcMs;
            }
            else
            {
                remainAdsRefreshTimeMs = getDailyRemainAdsRefreshTimeMs(catalog);
            }

            return CommonResult<CatalogRefreshState>.Success(
                new CatalogRefreshState(
                    serverNowUtcMs,
                    intervalMs,
                    nextCatalogRefreshUtcMs,
                    remainAutoRefreshTimeMs,
                    remainAdsRefreshTimeMs,
                    hasLimitedAdsOrFreeProductsInCatalog,
                    shouldRefreshCatalogProducts,
                    shouldRefillAdsFreeProducts,
                    shouldInitializeAutoRefreshUtcMs,
                    shouldClearAutoRefreshUtcMs,
                    shouldClearAdsRefreshUtcMs));
        }

        CommonResult<CatalogRefreshCycleOutcome> tryRefreshCatalog(
            SHOP_CATALOG_TYPE catalogType,
            bool requireServerTime,
            bool forceCatalogRefresh)
        {
            if (!isValidCatalogType(catalogType))
            {
                return CommonResult<CatalogRefreshCycleOutcome>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    $"Invalid shop catalog_type for refresh: {catalogType}");
            }

            if (!_catalogs.TryGetValue(catalogType, out var catalog) || catalog == null)
            {
                return CommonResult<CatalogRefreshCycleOutcome>.Success(
                    new CatalogRefreshCycleOutcome(false, false));
            }

            var evaluated = evaluateCatalogRefreshState(
                catalog,
                requireServerTime,
                forceCatalogRefresh);
            if (evaluated.IsFailure)
                return CommonResult<CatalogRefreshCycleOutcome>.Failure(evaluated.Error!);

            var refreshState = evaluated.Value;
            var didRefreshCatalogProducts = false;
            var didMutateStorage = false;
            var finalRemainAutoRefreshTimeMs = refreshState.RemainAutoRefreshTimeMs;
            var finalRemainAdsRefreshTimeMs = refreshState.RemainAdsRefreshTimeMs;

            if (refreshState.ShouldClearAutoRefreshUtcMs)
            {
                _storage.ClearAutoRefreshUtcMs(catalogType);
                finalRemainAutoRefreshTimeMs = 0L;
                didMutateStorage = true;
            }

            if (refreshState.ShouldRefreshCatalogProducts)
            {
                catalog.ClearRuntimeStateForRefresh(
                    clearAdsFreeRemainState: refreshState.ShouldRefillAdsFreeProducts);
                catalog.RefreshProducts();
                didRefreshCatalogProducts = true;
                didMutateStorage = true;

                if (refreshState.HasServerTime)
                {
                    if (catalog is ShopCatalogEvent)
                    {
                        if (refreshState.NextCatalogRefreshUtcMs > 0L)
                        {
                            _storage.SetAutoRefreshUtcMs(
                                catalogType,
                                refreshState.NextCatalogRefreshUtcMs);
                            finalRemainAutoRefreshTimeMs = getRemainingToNextRefreshMs(
                                refreshState.ServerNowUtcMs,
                                refreshState.NextCatalogRefreshUtcMs);
                        }
                        else
                        {
                            _storage.ClearAutoRefreshUtcMs(catalogType);
                            finalRemainAutoRefreshTimeMs = 0L;
                        }
                    }
                    else if (refreshState.AutoRefreshIntervalMs > 0L)
                    {
                        var nextAutoRefreshUtcMs = getNextRefreshUtcMs(
                            refreshState.ServerNowUtcMs,
                            refreshState.AutoRefreshIntervalMs);
                        if (nextAutoRefreshUtcMs > 0L)
                        {
                            _storage.SetAutoRefreshUtcMs(catalogType, nextAutoRefreshUtcMs);
                            finalRemainAutoRefreshTimeMs = getRemainingToNextRefreshMs(
                                refreshState.ServerNowUtcMs,
                                nextAutoRefreshUtcMs);
                        }
                        else
                        {
                            _storage.ClearAutoRefreshUtcMs(catalogType);
                            finalRemainAutoRefreshTimeMs = 0L;
                        }
                    }
                    else
                    {
                        _storage.ClearAutoRefreshUtcMs(catalogType);
                        finalRemainAutoRefreshTimeMs = 0L;
                    }
                }
                else
                {
                    _storage.ClearAutoRefreshUtcMs(catalogType);
                    finalRemainAutoRefreshTimeMs = 0L;
                }
            }
            else
            {
                if (refreshState.ShouldInitializeAutoRefreshUtcMs
                    && refreshState.HasServerTime
                    && refreshState.NextCatalogRefreshUtcMs > 0L)
                {
                    _storage.SetAutoRefreshUtcMs(
                        catalogType,
                        refreshState.NextCatalogRefreshUtcMs);
                    finalRemainAutoRefreshTimeMs = getRemainingToNextRefreshMs(
                        refreshState.ServerNowUtcMs,
                        refreshState.NextCatalogRefreshUtcMs);
                    didMutateStorage = true;
                }
            }

            if (refreshState.ShouldClearAdsRefreshUtcMs)
            {
                _storage.ClearAdsRefreshUtcMs(catalogType);
                finalRemainAdsRefreshTimeMs = 0L;
                didMutateStorage = true;
            }

            if (refreshState.ShouldRefillAdsFreeProducts)
            {
                var products = catalog.GetProducts();
                var didRefill = false;
                for (var i = 0; i < products.Count; i++)
                {
                    var product = products[i];
                    if (!isLimitedAdsOrFreeProduct(product))
                        continue;

                    product.ResetRemainCount();
                    var normalizedShopItemId = normalizeShopItemId(product.shop_item_id);
                    if (string.IsNullOrEmpty(normalizedShopItemId))
                        continue;

                    persistProductRemainState(product, normalizedShopItemId);
                    didRefill = true;
                }

                var previousAdsRefreshUtcMs = _storage.GetAdsRefreshUtcMs(catalogType);
                var nextAdsRefreshUtcMs = getNextRefreshUtcMs(
                    refreshState.ServerNowUtcMs,
                    MillisecondsPerDay);
                _storage.SetAdsRefreshUtcMs(catalogType, nextAdsRefreshUtcMs);
                finalRemainAdsRefreshTimeMs = getRemainingToNextRefreshMs(
                    refreshState.ServerNowUtcMs,
                    nextAdsRefreshUtcMs);
                if (didRefill || previousAdsRefreshUtcMs != nextAdsRefreshUtcMs)
                    didMutateStorage = true;
            }

            catalog.SetRemainAutoRefreshTimeMs(finalRemainAutoRefreshTimeMs);
            if (catalog is ShopCatalogDaily dailyCatalog)
                dailyCatalog.SetRemainAdsRefreshTimeMs(finalRemainAdsRefreshTimeMs);

            return CommonResult<CatalogRefreshCycleOutcome>.Success(
                new CatalogRefreshCycleOutcome(didRefreshCatalogProducts, didMutateStorage));
        }

        CommonResult<CatalogRefreshCycleOutcome> tryRefreshAllCatalogs(bool requireServerTime)
        {
            var catalogCount = _catalogList.Count;
            if (catalogCount <= 0)
            {
                return CommonResult<CatalogRefreshCycleOutcome>.Success(
                    new CatalogRefreshCycleOutcome(false, false));
            }

            var catalogTypes = new SHOP_CATALOG_TYPE[catalogCount];
            for (var i = 0; i < catalogCount; i++)
                catalogTypes[i] = _catalogList[i].CatalogType;

            var didRefreshAnyCatalogProducts = false;
            var didMutateAnyStorage = false;
            for (var i = 0; i < catalogTypes.Length; i++)
            {
                var catalogType = catalogTypes[i];
                if (!isValidCatalogType(catalogType))
                    continue;

                var refresh = tryRefreshCatalog(
                    catalogType,
                    requireServerTime,
                    forceCatalogRefresh: false);
                if (refresh.IsFailure)
                    return CommonResult<CatalogRefreshCycleOutcome>.Failure(refresh.Error!);

                if (refresh.Value.DidRefreshCatalogProducts)
                    didRefreshAnyCatalogProducts = true;
                if (refresh.Value.DidMutateStorage)
                    didMutateAnyStorage = true;
            }

            return CommonResult<CatalogRefreshCycleOutcome>.Success(
                new CatalogRefreshCycleOutcome(didRefreshAnyCatalogProducts, didMutateAnyStorage));
        }

        CommonResult initializeCore(bool requireServerTime)
        {
            var didInitializeCatalogs = ensureCatalogInitialized();
            if (!_catalogInitialized || _catalogList.Count <= 0)
            {
                _initialized = false;
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.SAVEDATA_SYNC_REQUIRED,
                    $"TB_SHOP_CATALOG is not ready. catalogCount={_catalogList.Count}");
            }

            var wasInitialized = _initialized;
            var refresh = refreshProductsCore(requireServerTime, refreshLockState: true);
            if (refresh.IsFailure)
            {
                _initialized = wasInitialized;
                return refresh;
            }

            if (didInitializeCatalogs)
            {
                synchronizeProductIndexFromCatalogs();
                queueRuntimeLocalSave();
            }

            _initialized = true;
            return CommonResult.Ok();
        }

        CommonResult refreshProductsCore(bool requireServerTime, bool refreshLockState)
        {
            if (!_catalogInitialized || _catalogList.Count <= 0)
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.SAVEDATA_SYNC_REQUIRED,
                    "ShopManager.Initialize must be called before refresh.");
            }

            var refresh = tryRefreshAllCatalogs(requireServerTime);
            if (refresh.IsFailure)
                return CommonResult.Failure(refresh.Error!);

            var didRefreshCatalogProducts = refresh.Value.DidRefreshCatalogProducts;
            var didMutateStorage = refresh.Value.DidMutateStorage;

            if (didRefreshCatalogProducts)
            {
                synchronizeProductIndexFromCatalogs();
                didMutateStorage = true;
            }

            var syncCatalogRuntime = syncCatalogRuntimeStates(requireServerTime);
            if (syncCatalogRuntime.IsFailure)
                return CommonResult.Failure(syncCatalogRuntime.Error!);

            if (syncCatalogRuntime.Value)
                didMutateStorage = true;

            if (didMutateStorage)
                queueRuntimeLocalSave();

            if (refreshLockState)
                refreshCatalogLockState();

            return CommonResult.Ok();
        }

        bool ensureCatalogInitialized()
        {
            if (_catalogInitialized && _catalogList.Count > 0)
                return false;

            if (_catalogInitialized && _catalogList.Count <= 0)
                _catalogInitialized = false;

            unSubscribeCatalogUnlockMessages();
            _catalogs.Clear();
            _catalogList.Clear();
            _productsByShopItemId.Clear();
            _limitedShopItemIdsByCatalog.Clear();

            var sourceCatalogs = ShopCatalogFactory.CreateRuntimeCatalogs(_storage);
            for (var i = 0; i < sourceCatalogs.Count; i++)
                addCatalog(sourceCatalogs[i]);

            for (var i = 0; i < _catalogList.Count; i++)
                _catalogList[i]?.Initialize();

            _catalogInitialized = _catalogList.Count > 0;
            if (!_catalogInitialized)
            {
                Debug.LogWarning(
                    $"[{Tag}] TB_SHOP_CATALOG is empty or not loaded yet. Catalog initialization will retry on next call.");
            }

            return _catalogInitialized;
        }

        void addCatalog(ShopCatalogBase sourceCatalog)
        {
            if (sourceCatalog == null)
                return;

            var catalogType = sourceCatalog.CatalogType;
            if (!isValidCatalogType(catalogType))
                return;

            if (_catalogs.ContainsKey(catalogType))
            {
                Debug.LogWarning($"[{Tag}] Duplicate catalog type. Keeping first catalog: {catalogType}");
                return;
            }

            _catalogs[catalogType] = sourceCatalog;
            _catalogList.Add(sourceCatalog);
        }

        void synchronizeProductIndexFromCatalogs()
        {
            _productsByShopItemId.Clear();
            _limitedShopItemIdsByCatalog.Clear();

            for (var i = 0; i < _catalogList.Count; i++)
            {
                var catalog = _catalogList[i];
                if (catalog == null)
                    continue;

                var products = catalog.GetProducts();
                for (var j = 0; j < products.Count; j++)
                {
                    var product = products[j];
                    if (product == null || string.IsNullOrWhiteSpace(product.shop_item_id))
                        continue;

                    var normalizedShopItemId = normalizeShopItemId(product.shop_item_id);
                    if (string.IsNullOrEmpty(normalizedShopItemId))
                        continue;

                    if (_productsByShopItemId.ContainsKey(normalizedShopItemId))
                    {
                        Debug.LogWarning(
                            $"[{Tag}] Duplicate shop_item_id across catalogs. Keeping first row: shop_item_id={normalizedShopItemId}, catalog={catalog.CatalogType}");
                        continue;
                    }

                    _productsByShopItemId.Add(normalizedShopItemId, product);
                    registerLimitedProduct(product, normalizedShopItemId);
                }
            }
        }

        void refreshCatalogLockState()
        {
            unSubscribeCatalogUnlockMessages();

            if (_catalogList.Count <= 0)
                return;

            if (!tryGetInitializedGameMessageManager(out var messageManager))
            {
                for (var i = 0; i < _catalogList.Count; i++)
                {
                    var catalog = _catalogList[i];
                    if (catalog == null)
                        continue;

                    catalog.SetLocked(hasCatalogUnlockCondition(catalog));
                }

                return;
            }

            var subscribedMessageTypes = new HashSet<GAME_MESSAGE_TYPE>();
            for (var i = 0; i < _catalogList.Count; i++)
            {
                var catalog = _catalogList[i];
                if (catalog == null)
                    continue;

                if (!hasCatalogUnlockCondition(catalog))
                {
                    catalog.SetLocked(false);
                    continue;
                }

                var shouldLock = true;
                if (tryResolveUnlockMessageRow(catalog, out var messageRow))
                {
                    var progress = messageManager.GetStat(catalog.unlock_msg_id);
                    var unlocked = GameMessageRule.IsConditionSatisfied(
                        progress,
                        catalog.unlock_op_type,
                        catalog.unlock_value);
                    shouldLock = !unlocked;

                    if (shouldLock
                        && messageRow != null
                        && messageRow.message_type != GAME_MESSAGE_TYPE.NONE
                        && subscribedMessageTypes.Add(messageRow.message_type))
                    {
                        messageManager.SubcribeGameMessageTrigger(
                            CatalogUnlockOwnerKey,
                            messageRow.message_type,
                            onCatalogUnlockMessageTriggered);
                    }
                }

                catalog.SetLocked(shouldLock);
            }

            _isCatalogUnlockSubscribed = subscribedMessageTypes.Count > 0;
        }

        bool tryResolveUnlockMessageRow(ShopCatalogBase catalog, out GAME_MESSAGE messageRow)
        {
            messageRow = null;
            if (!hasCatalogUnlockCondition(catalog))
                return false;

            var unlockMsgId = catalog.unlock_msg_id;
            if (string.IsNullOrWhiteSpace(unlockMsgId))
                return false;

            messageRow = TB_GAME_MESSAGE.Get(unlockMsgId);
            if (messageRow != null)
                return true;

            Debug.LogWarning(
                $"[{Tag}] SHOP_CATALOG unlock message is not found: catalog={catalog.CatalogType}, unlock_msg_id={unlockMsgId}");
            return false;
        }

        bool onCatalogUnlockMessageTriggered(object[] args)
        {
            refreshCatalogLockState();
            return false;
        }

        void unSubscribeCatalogUnlockMessages()
        {
            if (!_isCatalogUnlockSubscribed)
                return;

            if (!GameMessageManager.TryGet(out var messageManager) || messageManager == null)
            {
                _isCatalogUnlockSubscribed = false;
                return;
            }

            messageManager.UnSubcribeGameMessageTrigger(CatalogUnlockOwnerKey);
            _isCatalogUnlockSubscribed = false;
        }

        static bool tryGetInitializedGameMessageManager(out GameMessageManager messageManager)
        {
            if (!GameMessageManager.TryGet(out messageManager) || messageManager == null)
                return false;

            return messageManager.IsInitialized;
        }

        void registerLimitedProduct(ShopProductBase product, string normalizedShopItemId)
        {
            if (product == null || !product.HasPurchaseLimit || string.IsNullOrWhiteSpace(normalizedShopItemId))
                return;

            if (!_limitedShopItemIdsByCatalog.TryGetValue(product.catalog_type, out var shopItemIds))
            {
                shopItemIds = new List<string>();
                _limitedShopItemIdsByCatalog[product.catalog_type] = shopItemIds;
            }

            shopItemIds.Add(normalizedShopItemId);
        }

        void markProductPurchased(ShopProductBase product)
        {
            if (product == null || !product.HasPurchaseLimit)
                return;

            product.TryConsumeOne();

            var normalizedShopItemId = normalizeShopItemId(product.shop_item_id);
            if (string.IsNullOrEmpty(normalizedShopItemId))
                return;

            persistProductRemainState(product, normalizedShopItemId);
        }

        void markAdsRefreshOnPurchaseIfNeeded(ShopProductBase product)
        {
            if (product == null)
                return;

            if (product.ProductType != SHOP_PRODUCT_TYPE.ADS
                && product.ProductType != SHOP_PRODUCT_TYPE.FREE)
            {
                return;
            }

            if (!isValidCatalogType(product.catalog_type))
                return;

            var serverNowUtcMs = RemoteDataManager.ServerNowUtcMs;
            if (serverNowUtcMs <= 0L)
                return;

            _storage.SetAdsRefreshUtcMs(
                product.catalog_type,
                getNextRefreshUtcMs(serverNowUtcMs, MillisecondsPerDay));
        }

        void persistProductRemainState(ShopProductBase product, string normalizedShopItemId)
        {
            if (product == null || string.IsNullOrWhiteSpace(normalizedShopItemId))
                return;

            if (product.catalog_type == SHOP_CATALOG_TYPE.DAILY)
            {
                if (isDailyStoredProduct(product))
                {
                    _storage.RemoveProductRemainCount(product.catalog_type, normalizedShopItemId);
                    _storage.UpsertDailyCatalogProduct(
                        normalizedShopItemId,
                        product.DiscountType,
                        product.RemainCount);
                }
                else
                {
                    _storage.RemoveDailyCatalogProduct(normalizedShopItemId);
                    if (product.HasPurchaseLimit)
                        _storage.SetProductRemainCount(product.catalog_type, normalizedShopItemId, product.RemainCount);
                    else
                        _storage.RemoveProductRemainCount(product.catalog_type, normalizedShopItemId);
                }

                return;
            }

            if (!product.HasPurchaseLimit)
            {
                _storage.RemoveProductRemainCount(product.catalog_type, normalizedShopItemId);
                return;
            }

            _storage.SetProductRemainCount(product.catalog_type, normalizedShopItemId, product.RemainCount);
        }

        static bool isLimitedAdsOrFreeProduct(ShopProductBase product)
        {
            return product != null
                && product.HasPurchaseLimit
                && (product.ProductType == SHOP_PRODUCT_TYPE.ADS
                    || product.ProductType == SHOP_PRODUCT_TYPE.FREE);
        }

        static bool hasLimitedAdsOrFreeProducts(IReadOnlyList<ShopProductBase> products)
        {
            if (products == null || products.Count <= 0)
                return false;

            for (var i = 0; i < products.Count; i++)
            {
                if (isLimitedAdsOrFreeProduct(products[i]))
                    return true;
            }

            return false;
        }

        static long getDailyRemainAdsRefreshTimeMs(ShopCatalogBase catalog)
        {
            return catalog is ShopCatalogDaily dailyCatalog
                ? dailyCatalog.RemainAdsRefreshTimeMs
                : 0L;
        }

        static bool hasCatalogUnlockCondition(ShopCatalogBase catalog)
        {
            if (catalog == null)
                return false;

            return !string.IsNullOrWhiteSpace(catalog.unlock_msg_id);
        }

        static bool isDailyStoredProduct(ShopProductBase product)
        {
            if (product == null || product.catalog_type != SHOP_CATALOG_TYPE.DAILY)
                return false;

            return product.ProductType != SHOP_PRODUCT_TYPE.ADS
                && product.ProductType != SHOP_PRODUCT_TYPE.FREE;
        }

        CommonResult<ChestPurchaseState> resolveChestPurchaseRuntime(ShopProductChest product)
        {
            if (product == null)
            {
                return CommonResult<ChestPurchaseState>.Failure(
                    COMMON_ERROR_TYPE.SHOP_PRODUCT_NOT_FOUND,
                    "Chest product is null.");
            }

            var chestCatalog = GetCatalog<ShopCatalogChest>();
            if (chestCatalog == null)
            {
                return CommonResult<ChestPurchaseState>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    $"Chest catalog is unavailable: shop_item_id={product.shop_item_id}");
            }

            var resolveRuntime = chestCatalog.ResolvePurchaseRuntime(product);
            if (resolveRuntime.IsFailure)
                return CommonResult<ChestPurchaseState>.Failure(resolveRuntime.Error!);

            return CommonResult<ChestPurchaseState>.Success(
                new ChestPurchaseState(chestCatalog, resolveRuntime.Value));
        }

        CommonResult<ShopProductBase> checkStandardShopPurchaseCanBuy(
            ShopProductBase product,
            CURRENCY_TYPE currencyType)
        {
            if (product == null)
            {
                return CommonResult<ShopProductBase>.Failure(
                    COMMON_ERROR_TYPE.SHOP_PRODUCT_NOT_FOUND,
                    "Shop product is null.");
            }

            if (product.ProductType == SHOP_PRODUCT_TYPE.FREE)
                return CommonResult<ShopProductBase>.Success(product);

            if (product.ProductType == SHOP_PRODUCT_TYPE.ADS)
            {
                if (tryGetNoAdsRentalRemainingMs(out var noAdsRentalRemainingMs)
                    && noAdsRentalRemainingMs > 0L)
                {
                    return CommonResult<ShopProductBase>.Success(product);
                }

                try
                {
                    if (!AdsManager.Instance.CanShow())
                    {
                        return CommonResult<ShopProductBase>.Failure(
                            COMMON_ERROR_TYPE.SHOP_ADS_NOT_AVAILABLE,
                            $"Shop rewarded ad is not available: shop_item_id={product.shop_item_id}");
                    }
                }
                catch (Exception ex)
                {
                    return CommonResult<ShopProductBase>.Failure(
                        COMMON_ERROR_TYPE.SHOP_ADS_NOT_AVAILABLE,
                        $"Shop rewarded ad check failed: shop_item_id={product.shop_item_id}, reason={ex.Message}");
                }

                return CommonResult<ShopProductBase>.Success(product);
            }

            if (!tryGetWallet(out var wallet) || wallet == null)
            {
                return CommonResult<ShopProductBase>.Failure(
                    COMMON_ERROR_TYPE.SHOP_WALLET_UNAVAILABLE,
                    $"Inventory wallet is unavailable: shop_item_id={product.shop_item_id}");
            }

            var price = product.Price;
            if (!hasSufficientCurrency(wallet, currencyType, price))
            {
                return CommonResult<ShopProductBase>.Failure(
                    COMMON_ERROR_TYPE.SHOP_CURRENCY_INSUFFICIENT,
                    $"Insufficient currency for shop purchase: shop_item_id={product.shop_item_id}, currency={currencyType}, price={price}, basePrice={product.PriceWithoutDiscount}, discountType={product.DiscountType}");
            }

            return CommonResult<ShopProductBase>.Success(product);
        }

        static bool isValidCatalogType(SHOP_CATALOG_TYPE catalogType)
        {
            return catalogType != SHOP_CATALOG_TYPE.NONE;
        }

        static long getAutoRefreshIntervalMs(int autoRefreshDays)
        {
            if (autoRefreshDays <= 0)
                return 0L;

            return MillisecondsPerDay * autoRefreshDays;
        }

        static long getRemainingToNextRefreshMs(long serverNowUtcMs, long nextRefreshUtcMs)
        {
            if (serverNowUtcMs <= 0L || nextRefreshUtcMs <= 0L)
                return 0L;

            if (serverNowUtcMs >= nextRefreshUtcMs)
                return 0L;

            return nextRefreshUtcMs - serverNowUtcMs;
        }

        static long getNextRefreshUtcMs(long serverNowUtcMs, long refreshIntervalMs)
        {
            if (serverNowUtcMs <= 0L || refreshIntervalMs <= 0L)
                return 0L;

            if (long.MaxValue - serverNowUtcMs < refreshIntervalMs)
                return long.MaxValue;

            return serverNowUtcMs + refreshIntervalMs;
        }

        CommonResult<bool> syncCatalogRuntimeStates(bool requireServerTime)
        {
            var didMutateStorage = false;
            for (var i = 0; i < _catalogList.Count; i++)
            {
                var catalog = _catalogList[i];
                if (catalog == null)
                    continue;

                var sync = catalog.SyncRuntimeState(requireServerTime);
                if (sync.IsFailure)
                    return CommonResult<bool>.Failure(sync.Error!);

                if (sync.Value)
                    didMutateStorage = true;
            }

            return CommonResult<bool>.Success(didMutateStorage);
        }

        static CommonResult<RewardData[]> wrapBuyFailure(string shopItemId, CommonError innerError)
        {
            var normalizedShopItemId = normalizeShopItemId(shopItemId);
            if (innerError.Code == COMMON_ERROR_TYPE.SHOP_CURRENCY_INSUFFICIENT)
            {
                return CommonResult<RewardData[]>.Failure(
                    COMMON_ERROR_TYPE.SHOP_CURRENCY_INSUFFICIENT,
                    $"Insufficient currency for shop purchase: shop_item_id={normalizedShopItemId}, inner={innerError.Code}:{innerError.Message}");
            }

            return CommonResult<RewardData[]>.Failure(
                COMMON_ERROR_TYPE.SHOP_BUY_FAILED,
                $"Shop BuyAsync failed: shop_item_id={normalizedShopItemId}, inner={innerError.Code}:{innerError.Message}");
        }

        static bool tryResolvePurchaseProduct(string internalProductId, out PURCHASE purchaseProduct)
        {
            purchaseProduct = null;
            if (string.IsNullOrWhiteSpace(internalProductId))
                return false;

            purchaseProduct = TB_PURCHASE.Get(internalProductId.Trim());
            return purchaseProduct != null;
        }

        CommonResult<RewardData[]> applyShopProductRewards(string rewardGroupId, int amount)
        {
            var repeatCount = amount < 1 ? 1 : amount;
            var grantedRewards = new List<RewardData>(repeatCount);

            for (var i = 0; i < repeatCount; i++)
            {
                var apply = RewardManager.Instance.ApplyRewardGroup(rewardGroupId);
                if (apply.IsFailure)
                {
                    rollbackGrantedShopRewards(grantedRewards, rewardGroupId, i);
                    return CommonResult<RewardData[]>.Failure(apply.Error!);
                }

                var applied = apply.Value.AppliedRewards;
                if (applied == null || applied.Length == 0)
                    continue;

                grantedRewards.AddRange(applied);
            }

            return CommonResult<RewardData[]>.Success(normalizeShopRewards(grantedRewards));
        }

        static RewardData[] normalizeShopRewards(IReadOnlyList<RewardData> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<RewardData>();

            var normalized = new List<RewardData>(source.Count);
            var nonEquipIndices = new Dictionary<(REWARD_TYPE Type, string Id), int>();

            for (var i = 0; i < source.Count; i++)
            {
                var reward = source[i];
                if (reward.Amount <= 0 || string.IsNullOrWhiteSpace(reward.Id))
                    continue;

                if (reward.Type == REWARD_TYPE.EQUIP)
                {
                    for (var equipCount = 0; equipCount < reward.Amount; equipCount++)
                        normalized.Add(new RewardData(REWARD_TYPE.EQUIP, reward.Id, 1));
                    continue;
                }

                var key = (reward.Type, reward.Id);
                if (!nonEquipIndices.TryGetValue(key, out var index))
                {
                    nonEquipIndices.Add(key, normalized.Count);
                    normalized.Add(reward);
                    continue;
                }

                var existing = normalized[index];
                var amountLong = (long)existing.Amount + reward.Amount;
                var amount = amountLong > int.MaxValue ? int.MaxValue : (int)amountLong;
                normalized[index] = new RewardData(existing.Type, existing.Id, amount);
            }

            return normalized.Count == 0 ? Array.Empty<RewardData>() : normalized.ToArray();
        }

        static void rollbackGrantedShopRewards(List<RewardData> grantedRewards, string rewardGroupId, int appliedLoopCount)
        {
            if (grantedRewards == null || grantedRewards.Count == 0)
                return;

            try
            {
                var rollback = RewardManager.Instance.RevokeRewardDatas(grantedRewards.ToArray());
                if (rollback.IsFailure)
                {
                    Debug.LogError(
                        $"[{Tag}] Shop reward rollback failed after apply failure: reward_group_id={rewardGroupId}, appliedLoopCount={appliedLoopCount}, grantedRewardCount={grantedRewards.Count}, reason={rollback.Error}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[{Tag}] Shop reward rollback threw exception: reward_group_id={rewardGroupId}, appliedLoopCount={appliedLoopCount}, grantedRewardCount={grantedRewards.Count}, exception={ex.Message}");
            }
        }

        static bool tryDeductCurrency(
            InventoryWallet wallet,
            CURRENCY_TYPE currencyType,
            int price,
            out CurrencyDeduction deduction)
        {
            deduction = default;

            if (price < 0)
                return false;

            if (price == 0)
                return true;

            if (currencyType == CURRENCY_TYPE.FREE)
                return false;

            if (currencyType == CURRENCY_TYPE.ADS)
                return false;

            if (currencyType == CURRENCY_TYPE.JEWEL)
            {
                var free = wallet.Get(CURRENCY_TYPE.JEWEL_FREE);
                var paid = wallet.Get(CURRENCY_TYPE.JEWEL_PAID);
                var total = free + paid;
                if (total < price)
                    return false;

                var useFree = Math.Min((long)price, free);
                var usePaid = price - useFree;
                if (useFree > 0 && !wallet.TryAdd(CURRENCY_TYPE.JEWEL_FREE, -useFree))
                    return false;

                if (usePaid > 0 && !wallet.TryAdd(CURRENCY_TYPE.JEWEL_PAID, -usePaid))
                {
                    if (useFree > 0)
                        wallet.TryAdd(CURRENCY_TYPE.JEWEL_FREE, useFree);
                    return false;
                }

                deduction = new CurrencyDeduction(CURRENCY_TYPE.JEWEL, useFree, usePaid, 0L);
                return true;
            }

            var balance = wallet.Get(currencyType);
            if (balance < price)
                return false;

            if (!wallet.TryAdd(currencyType, -price))
                return false;

            deduction = new CurrencyDeduction(currencyType, 0L, 0L, price);
            return true;
        }

        static void rollbackCurrency(InventoryWallet wallet, CurrencyDeduction deduction)
        {
            if (deduction.CurrencyType == CURRENCY_TYPE.JEWEL)
            {
                if (deduction.DeductJewelFree > 0 &&
                    !wallet.TryAdd(CURRENCY_TYPE.JEWEL_FREE, deduction.DeductJewelFree))
                {
                    Debug.LogError($"[{Tag}] Failed to rollback JEWEL_FREE deduction.");
                }

                if (deduction.DeductJewelPaid > 0 &&
                    !wallet.TryAdd(CURRENCY_TYPE.JEWEL_PAID, deduction.DeductJewelPaid))
                {
                    Debug.LogError($"[{Tag}] Failed to rollback JEWEL_PAID deduction.");
                }

                return;
            }

            if (deduction.DeductAmount > 0 &&
                !wallet.TryAdd(deduction.CurrencyType, deduction.DeductAmount))
            {
                Debug.LogError($"[{Tag}] Failed to rollback currency deduction. currency={deduction.CurrencyType}");
            }
        }

        static bool hasSufficientCurrency(InventoryWallet wallet, CURRENCY_TYPE currencyType, int price)
        {
            if (price < 0)
                return false;

            if (price == 0)
                return true;

            if (currencyType == CURRENCY_TYPE.FREE)
                return false;

            if (currencyType == CURRENCY_TYPE.ADS)
                return false;

            if (currencyType == CURRENCY_TYPE.JEWEL)
            {
                var free = wallet.Get(CURRENCY_TYPE.JEWEL_FREE);
                var paid = wallet.Get(CURRENCY_TYPE.JEWEL_PAID);
                return free + paid >= price;
            }

            return wallet.Get(currencyType) >= price;
        }

        static bool tryGetWallet(out InventoryWallet wallet)
        {
            wallet = null;
            try
            {
                wallet = InventoryManager.Instance.Storage.Wallet;
                return wallet != null;
            }
            catch
            {
                return false;
            }
        }

        static bool tryGetNoAdsRentalRemainingMs(out long remainingMs)
        {
            remainingMs = 0L;
            try
            {
                remainingMs = InventoryManager.Instance.Storage.GetRentalRemainingMs(DefaultRentalNoAdsId);
                return true;
            }
            catch
            {
                return false;
            }
        }

        static string normalizeShopItemId(string shopItemId)
        {
            return shopItemId != null ? shopItemId.Trim() : string.Empty;
        }

        void queueRuntimeLocalSave()
        {
            lock (_runtimeSaveLock)
            {
                _runtimeSavePending = true;
                if (_runtimeSaveInFlight)
                    return;

                _runtimeSaveInFlight = true;
            }

            _ = flushRuntimeLocalSaveQueueAsync();
        }

        async Task flushRuntimeLocalSaveQueueAsync()
        {
            while (true)
            {
                lock (_runtimeSaveLock)
                {
                    if (!_runtimeSavePending)
                    {
                        _runtimeSaveInFlight = false;
                        return;
                    }

                    _runtimeSavePending = false;
                }

                CommonResult<bool> save;
                try
                {
                    save = await SaveDataManager.Instance.SaveGameStorageAsync(
                        saveCloud: false,
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[{Tag}] Runtime local save threw exception (non-fatal): {ex.Message}");
                    lock (_runtimeSaveLock)
                    {
                        _runtimeSaveInFlight = false;
                        _runtimeSavePending = false;
                    }
                    return;
                }

                if (save.IsSuccess)
                    continue;

                if (save.Error == null
                    || save.Error.Code != COMMON_ERROR_TYPE.SAVEDATA_SYNC_REQUIRED)
                {
                    Debug.LogWarning($"[{Tag}] Runtime local save failed (non-fatal): {save.Error}");
                }

                lock (_runtimeSaveLock)
                {
                    _runtimeSaveInFlight = false;
                    _runtimeSavePending = false;
                }
                return;
            }
        }

        readonly struct CurrencyDeduction
        {
            public CurrencyDeduction(
                CURRENCY_TYPE currencyType,
                long deductJewelFree,
                long deductJewelPaid,
                long deductAmount)
            {
                CurrencyType = currencyType;
                DeductJewelFree = deductJewelFree;
                DeductJewelPaid = deductJewelPaid;
                DeductAmount = deductAmount;
            }

            public bool HasDeduction =>
                CurrencyType == CURRENCY_TYPE.JEWEL
                    ? DeductJewelFree > 0 || DeductJewelPaid > 0
                    : DeductAmount > 0;

            public CURRENCY_TYPE CurrencyType { get; }
            public long DeductJewelFree { get; }
            public long DeductJewelPaid { get; }
            public long DeductAmount { get; }
        }
    }
}
