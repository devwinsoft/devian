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
        readonly Dictionary<string, ShopProductBase> _productsByShopId = new(StringComparer.Ordinal);
        readonly Dictionary<SHOP_CATALOG_TYPE, List<string>> _limitedShopIdsByCatalog = new();
        readonly object _runtimeSaveLock = new();

        bool _catalogInitialized;
        bool _initialized;
        bool _isCatalogUnlockSubscribed;
        bool _runtimeSavePending;
        bool _runtimeSaveInFlight;

        public ShopStorage Storage => _storage;

        protected override void onDestroy()
        {
            unSubscribeCatalogUnlockMessages();
        }

        public GameResult Initialize()
        {
            return initializeCore(requireServerTime: true);
        }

        public GameResult RefreshProducts(bool requireServerTime = true)
        {
            if (!_initialized || !_catalogInitialized || _catalogList.Count <= 0)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
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

        public GameResult<ShopProductBase> GetProduct(string shopId)
        {
            return validateShopProductConfig(shopId);
        }

        public GameResult<ShopRewardProductBase> GetRewardProduct(string shopId)
        {
            return getProductAs<ShopRewardProductBase>(shopId);
        }

        public GameResult<ShopLimitedProductBase> GetLimitedProduct(string shopId)
        {
            return getProductAs<ShopLimitedProductBase>(shopId);
        }

        public GameResult<ShopProductDaily> GetDailyProduct(string shopId)
        {
            return getProductAs<ShopProductDaily>(shopId);
        }

        public GameResult<ShopProductEvent> GetEventProduct(string shopId)
        {
            return getProductAs<ShopProductEvent>(shopId);
        }

        public GameResult<ShopProductGold> GetGoldProduct(string shopId)
        {
            return getProductAs<ShopProductGold>(shopId);
        }

        public GameResult<ShopProductChest> GetChestProduct(string shopId)
        {
            return getProductAs<ShopProductChest>(shopId);
        }

        public GameResult<ShopProductPurchase> GetPurchaseProduct(string shopId)
        {
            return getProductAs<ShopProductPurchase>(shopId);
        }

        internal void InvalidateRuntimeState()
        {
            unSubscribeCatalogUnlockMessages();
            _initialized = false;
            _catalogInitialized = false;
            _catalogs.Clear();
            _catalogList.Clear();
            _productsByShopId.Clear();
            _limitedShopIdsByCatalog.Clear();
        }

        public GAME_ERROR_TYPE CanBuy(string shopId)
        {
            var check = checkCanBuy(shopId);
            if (check.IsFailure)
                return check.Error?.Code ?? GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT;

            return GAME_ERROR_TYPE.SUCCESS;
        }

        public async Task<GameResult<RewardData[]>> BuyAsync(string shopId, CancellationToken ct = default)
        {
            var check = checkCanBuy(shopId);
            if (check.IsFailure)
                return wrapBuyFailure(shopId, check.Error!);

            var product = check.Value!;
            GameResult<RewardData[]> buyResult;
            if (product is ShopProductPurchase purchaseProduct)
                buyResult = await buyPurchaseCatalogAsync(purchaseProduct, ct);
            else if (product is ShopProductChest chestProduct)
                buyResult = await buyChestCatalogAsync(chestProduct, ct);
            else if (product is ShopRewardProductBase rewardProduct)
                buyResult = await buyRewardCatalogAsync(rewardProduct, ct);
            else
                buyResult = GameResult<RewardData[]>.Failure(
                    GAME_ERROR_TYPE.SHOP_PRODUCT_NOT_FOUND,
                    $"Shop product type is not supported: shop_id={product.shop_id}, productType={product.ProductType}");

            if (buyResult.IsFailure)
                return wrapBuyFailure(shopId, buyResult.Error!);

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

        GameResult<ShopProductBase> checkCanBuy(string shopId)
        {
            var validateProduct = validateShopProductConfig(shopId);
            if (validateProduct.IsFailure)
                return GameResult<ShopProductBase>.Failure(validateProduct.Error!);

            var product = validateProduct.Value!;

            if (product is ShopProductPurchase purchaseProduct)
            {
                var validateSeason = validateSeasonPurchaseWindow(purchaseProduct);
                if (validateSeason.IsFailure)
                    return GameResult<ShopProductBase>.Failure(validateSeason.Error!);

                return GameResult<ShopProductBase>.Success(product);
            }

            if (product is ShopProductChest chestProduct)
            {
                var resolveChest = resolveChestPurchaseRuntime(chestProduct);
                if (resolveChest.IsFailure)
                    return GameResult<ShopProductBase>.Failure(resolveChest.Error!);

                return checkStandardShopPurchaseCanBuy(chestProduct, chestProduct.currency_type);
            }

            if (product is not ShopRewardProductBase rewardProduct)
            {
                return GameResult<ShopProductBase>.Failure(
                    GAME_ERROR_TYPE.SHOP_PRODUCT_NOT_FOUND,
                    $"Shop product type is not supported: shop_id={product.shop_id}, productType={product.ProductType}");
            }

            return checkStandardShopPurchaseCanBuy(rewardProduct, rewardProduct.currency_type);
        }

        internal GameResult ResetAdsInternal(SHOP_CATALOG_TYPE catalogType)
        {
            if (!_initialized || !_catalogInitialized || _catalogList.Count <= 0)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "ShopManager.Initialize must be called before ResetAds.");
            }

            if (!isValidCatalogType(catalogType))
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"Invalid shop catalog_type for ResetAds: {catalogType}");
            }

            var refresh = tryRefreshCatalog(
                catalogType,
                requireServerTime: true,
                forceCatalogRefresh: true);
            if (refresh.IsFailure)
                return GameResult.Failure(refresh.Error!);

            if (refresh.Value.DidRefreshCatalogProducts)
                synchronizeProductIndexFromCatalogs();

            if (refresh.Value.DidMutateStorage || refresh.Value.DidRefreshCatalogProducts)
                queueRuntimeLocalSave();

            return GameResult.Ok();
        }

        GameResult<ShopProductBase> validateShopProductConfig(string shopId)
        {
            if (!_initialized || !_catalogInitialized || _catalogList.Count <= 0)
            {
                return GameResult<ShopProductBase>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "ShopManager.Initialize must be called before CanBuy/BuyAsync.");
            }

            var refresh = refreshProductsCore(requireServerTime: true, refreshLockState: false);
            if (refresh.IsFailure)
                return GameResult<ShopProductBase>.Failure(refresh.Error!);

            var normalizedShopId = normalizeShopId(shopId);
            if (string.IsNullOrEmpty(normalizedShopId))
            {
                return GameResult<ShopProductBase>.Failure(
                    GAME_ERROR_TYPE.SHOP_PRODUCT_ID_EMPTY,
                    "Shop shop_id is empty.");
            }

            synchronizeProductIndexFromCatalogs();

            if (!_productsByShopId.TryGetValue(normalizedShopId, out var product) || product == null)
            {
                return GameResult<ShopProductBase>.Failure(
                    GAME_ERROR_TYPE.SHOP_PRODUCT_NOT_FOUND,
                    $"Shop product not found: shop_id={normalizedShopId}");
            }

            if (_catalogs.TryGetValue(product.catalog_type, out var productCatalog)
                && productCatalog != null
                && productCatalog.IsLocked)
            {
                return GameResult<ShopProductBase>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"Shop catalog is locked: catalog_type={product.catalog_type}, shop_id={normalizedShopId}");
            }

            if (product is ShopRewardProductBase rewardProduct && rewardProduct.PriceWithoutDiscount < 0)
            {
                return GameResult<ShopProductBase>.Failure(
                    GAME_ERROR_TYPE.SHOP_PRODUCT_PRICE_INVALID,
                    $"Shop product price is invalid: shop_id={product.shop_id}, price={rewardProduct.PriceWithoutDiscount}, discountType={rewardProduct.DiscountType}");
            }

            if (product is ShopProductChest chestProduct && chestProduct.PriceWithoutDiscount < 0)
            {
                return GameResult<ShopProductBase>.Failure(
                    GAME_ERROR_TYPE.SHOP_PRODUCT_PRICE_INVALID,
                    $"Shop product price is invalid: shop_id={product.shop_id}, price={chestProduct.PriceWithoutDiscount}, discountType={chestProduct.DiscountType}");
            }

            if (product is ShopLimitedProductBase limitedProduct
                && limitedProduct.HasPurchaseLimit)
            {
                if (limitedProduct.max_count == 0)
                {
                    return GameResult<ShopProductBase>.Failure(
                        GAME_ERROR_TYPE.SHOP_ITEM_PURCHASE_LIMIT_DISABLED,
                        $"Shop purchase is disabled by max_count=0: shop_id={product.shop_id}");
                }

                if (limitedProduct.RemainCount <= 0)
                {
                    var usedCount = limitedProduct.max_count - limitedProduct.RemainCount;
                    return GameResult<ShopProductBase>.Failure(
                        GAME_ERROR_TYPE.SHOP_ITEM_PURCHASE_LIMIT_EXCEEDED,
                        $"Shop purchase limit exceeded: shop_id={product.shop_id}, max_count={limitedProduct.max_count}, remainCount={limitedProduct.RemainCount}, usedCount={usedCount}");
                }
            }

            if (product is ShopProductPurchase purchaseProduct)
            {
                if (string.IsNullOrWhiteSpace(purchaseProduct.internal_product_id))
                {
                    return GameResult<ShopProductBase>.Failure(
                        GAME_ERROR_TYPE.PURCHASE_NOT_FOUND,
                        $"Shop purchase internal_product_id is empty: shop_id={purchaseProduct.shop_id}");
                }

                if (!tryResolvePurchaseProduct(purchaseProduct.internal_product_id, out var purchaseRow) || purchaseRow == null)
                {
                    return GameResult<ShopProductBase>.Failure(
                        GAME_ERROR_TYPE.PURCHASE_NOT_FOUND,
                        $"Purchase product not found: shop_id={purchaseProduct.shop_id}, internal_product_id={purchaseProduct.internal_product_id}");
                }

                if (!purchaseRow.is_active)
                {
                    return GameResult<ShopProductBase>.Failure(
                        GAME_ERROR_TYPE.PURCHASE_NOT_FOUND,
                        $"Purchase product is inactive: shop_id={purchaseProduct.shop_id}, internal_product_id={purchaseProduct.internal_product_id}");
                }

                return GameResult<ShopProductBase>.Success(product);
            }

            if (product is ShopProductChest validateChestProduct)
            {
                if (validateChestProduct.ProductType == SHOP_PRODUCT_TYPE.ADS
                    && validateChestProduct.PriceWithoutDiscount != 0)
                {
                    return GameResult<ShopProductBase>.Failure(
                        GAME_ERROR_TYPE.SHOP_PRODUCT_PRICE_INVALID,
                        $"Shop pseudo product price must be zero: shop_id={validateChestProduct.shop_id}, productType={validateChestProduct.ProductType}, price={validateChestProduct.PriceWithoutDiscount}");
                }

                if (validateChestProduct.chest_type == SHOP_PRODUCT_CHEST_TYPE.NONE)
                {
                    return GameResult<ShopProductBase>.Failure(
                        GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                        $"Shop chest_type is invalid: shop_id={validateChestProduct.shop_id}");
                }

                var resolveChest = resolveChestPurchaseRuntime(validateChestProduct);
                if (resolveChest.IsFailure)
                    return GameResult<ShopProductBase>.Failure(resolveChest.Error!);

                return GameResult<ShopProductBase>.Success(product);
            }

            if (product is not ShopRewardProductBase validateRewardProduct)
            {
                return GameResult<ShopProductBase>.Failure(
                    GAME_ERROR_TYPE.SHOP_PRODUCT_NOT_FOUND,
                    $"Shop product type is not supported: shop_id={product.shop_id}, productType={product.ProductType}");
            }

            if (validateRewardProduct.ProductType == SHOP_PRODUCT_TYPE.FREE
                || validateRewardProduct.ProductType == SHOP_PRODUCT_TYPE.ADS)
            {
                if (validateRewardProduct.PriceWithoutDiscount != 0)
                {
                    return GameResult<ShopProductBase>.Failure(
                        GAME_ERROR_TYPE.SHOP_PRODUCT_PRICE_INVALID,
                        $"Shop pseudo product price must be zero: shop_id={validateRewardProduct.shop_id}, productType={validateRewardProduct.ProductType}, price={validateRewardProduct.PriceWithoutDiscount}");
                }
            }

            if (string.IsNullOrWhiteSpace(validateRewardProduct.reward_group_id))
            {
                return GameResult<ShopProductBase>.Failure(
                    GAME_ERROR_TYPE.SHOP_REWARD_GROUP_EMPTY,
                    $"Shop product reward_group_id is empty: shop_id={product.shop_id}");
            }

            return GameResult<ShopProductBase>.Success(product);
        }

        GameResult<TProduct> getProductAs<TProduct>(string shopId) where TProduct : ShopProductBase
        {
            var validated = validateShopProductConfig(shopId);
            if (validated.IsFailure)
                return GameResult<TProduct>.Failure(validated.Error!);

            if (validated.Value is TProduct typedProduct)
                return GameResult<TProduct>.Success(typedProduct);

            return GameResult<TProduct>.Failure(
                GAME_ERROR_TYPE.SHOP_PRODUCT_NOT_FOUND,
                $"Shop product type is not supported: shop_id={shopId}, expected={typeof(TProduct).Name}, actual={validated.Value?.GetType().Name ?? nameof(ShopProductBase)}");
        }

        async Task<GameResult<RewardData[]>> buyPurchaseCatalogAsync(ShopProductPurchase product, CancellationToken ct)
        {
            var validateSeason = validateSeasonPurchaseWindow(product);
            if (validateSeason.IsFailure)
                return GameResult<RewardData[]>.Failure(validateSeason.Error!);

            var purchaseResult = await PurchaseManager.Instance.PurchaseAsync(product.internal_product_id, ct);
            if (purchaseResult.IsFailure)
                return GameResult<RewardData[]>.Failure(purchaseResult.Error!);

            var save = await SaveDataManager.Instance.SaveGameStorageAsync(true, ct);
            if (save.IsFailure)
                Debug.LogWarning($"[{Tag}] Post-shop purchase save failed (non-fatal): {save.Error}");

            var applied = purchaseResult.Value.AppliedRewards ?? Array.Empty<RewardData>();
            return GameResult<RewardData[]>.Success(applied);
        }

        async Task<GameResult<RewardData[]>> buyChestCatalogAsync(ShopProductChest product, CancellationToken ct)
        {
            var resolveChest = resolveChestPurchaseRuntime(product);
            if (resolveChest.IsFailure)
                return GameResult<RewardData[]>.Failure(resolveChest.Error!);

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
                        return GameResult<RewardData[]>.Failure(
                            GAME_ERROR_TYPE.SHOP_ADS_SHOW_FAILED,
                            $"Shop rewarded ad show failed: shop_id={product.shop_id}, {details}");
                    }
                }
            }
            else if (product.ProductType == SHOP_PRODUCT_TYPE.CURRENCY)
            {
                if (!tryGetWallet(out wallet) || wallet == null)
                {
                    return GameResult<RewardData[]>.Failure(
                        GAME_ERROR_TYPE.SHOP_WALLET_UNAVAILABLE,
                        $"Inventory wallet is unavailable: shop_id={product.shop_id}");
                }

                var price = product.Price;
                if (price < 0)
                {
                    return GameResult<RewardData[]>.Failure(
                        GAME_ERROR_TYPE.SHOP_PRODUCT_PRICE_INVALID,
                        $"Shop product price is invalid: shop_id={product.shop_id}, price={price}, basePrice={product.PriceWithoutDiscount}, discountType={product.DiscountType}");
                }

                if (!tryDeductCurrency(wallet, product.currency_type, price, out deduction))
                {
                    return GameResult<RewardData[]>.Failure(
                        GAME_ERROR_TYPE.SHOP_CURRENCY_INSUFFICIENT,
                        $"Insufficient currency for shop purchase: shop_id={product.shop_id}, currency={product.currency_type}, price={price}, basePrice={product.PriceWithoutDiscount}, discountType={product.DiscountType}");
                }
            }
            else
            {
                return GameResult<RewardData[]>.Failure(
                    GAME_ERROR_TYPE.SHOP_PRODUCT_NOT_FOUND,
                    $"Shop chest product type is not supported: shop_id={product.shop_id}, productType={product.ProductType}, chest_type={product.chest_type}");
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
                return GameResult<RewardData[]>.Failure(
                    GAME_ERROR_TYPE.SHOP_REWARD_APPLY_FAILED,
                    $"Shop chest reward apply failed: shop_id={product.shop_id}, reward_group_id={runtime.RewardGroupId}, {details}");
            }

            chestState.Catalog.AddExp(runtime.GainExp);

            if (product.HasPurchaseLimit)
                markProductPurchased(product);

            markAdsRefreshOnPurchaseIfNeeded(product);

            var save = await SaveDataManager.Instance.SaveGameStorageAsync(true, ct);
            if (save.IsFailure)
                Debug.LogWarning($"[{Tag}] Post-shop chest purchase save failed (non-fatal): {save.Error}");

            var applied = applyRewards.Value ?? Array.Empty<RewardData>();
            return GameResult<RewardData[]>.Success(applied);
        }

        async Task<GameResult<RewardData[]>> buyRewardCatalogAsync(ShopRewardProductBase product, CancellationToken ct)
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
                        return GameResult<RewardData[]>.Failure(
                            GAME_ERROR_TYPE.SHOP_ADS_SHOW_FAILED,
                            $"Shop rewarded ad show failed: shop_id={product.shop_id}, {details}");
                    }
                }
            }
            else if (product.ProductType == SHOP_PRODUCT_TYPE.CURRENCY)
            {
                if (!tryGetWallet(out wallet) || wallet == null)
                {
                    return GameResult<RewardData[]>.Failure(
                        GAME_ERROR_TYPE.SHOP_WALLET_UNAVAILABLE,
                        $"Inventory wallet is unavailable: shop_id={product.shop_id}");
                }

                var price = product.Price;
                if (price < 0)
                {
                    return GameResult<RewardData[]>.Failure(
                        GAME_ERROR_TYPE.SHOP_PRODUCT_PRICE_INVALID,
                        $"Shop product price is invalid: shop_id={product.shop_id}, price={price}, basePrice={product.PriceWithoutDiscount}, discountType={product.DiscountType}");
                }

                if (!tryDeductCurrency(wallet, product.currency_type, price, out deduction))
                {
                    return GameResult<RewardData[]>.Failure(
                        GAME_ERROR_TYPE.SHOP_CURRENCY_INSUFFICIENT,
                        $"Insufficient currency for shop purchase: shop_id={product.shop_id}, currency={product.currency_type}, price={price}, basePrice={product.PriceWithoutDiscount}, discountType={product.DiscountType}");
                }
            }
            else if (product.ProductType != SHOP_PRODUCT_TYPE.FREE)
            {
                return GameResult<RewardData[]>.Failure(
                    GAME_ERROR_TYPE.SHOP_PRODUCT_NOT_FOUND,
                    $"Shop product type is not supported: shop_id={product.shop_id}, productType={product.ProductType}");
            }

            var applyRewards = applyShopProductRewards(product.reward_group_id, product.amount);
            if (applyRewards.IsFailure)
            {
                if (deduction.HasDeduction)
                    rollbackCurrency(wallet, deduction);

                var details = applyRewards.Error != null
                    ? $"inner={applyRewards.Error.Code}:{applyRewards.Error.Message}"
                    : "inner=unknown";
                return GameResult<RewardData[]>.Failure(
                    GAME_ERROR_TYPE.SHOP_REWARD_APPLY_FAILED,
                    $"Shop reward apply failed: shop_id={product.shop_id}, reward_group_id={product.reward_group_id}, {details}");
            }

            if (product.HasPurchaseLimit)
                markProductPurchased(product);

            markAdsRefreshOnPurchaseIfNeeded(product);

            var save = await SaveDataManager.Instance.SaveGameStorageAsync(true, ct);
            if (save.IsFailure)
                Debug.LogWarning($"[{Tag}] Post-shop purchase save failed (non-fatal): {save.Error}");

            var applied = applyRewards.Value ?? Array.Empty<RewardData>();
            return GameResult<RewardData[]>.Success(applied);
        }

        GameResult validateSeasonPurchaseWindow(ShopProductPurchase product)
        {
            if (!tryResolvePurchaseProduct(product.internal_product_id, out var purchaseProduct) || purchaseProduct == null)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.PURCHASE_NOT_FOUND,
                    $"Purchase product not found: shop_id={product.shop_id}, internal_product_id={product.internal_product_id}");
            }

            var seasonId = (product.season_id ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(seasonId))
                return GameResult.Ok();

            var season = TB_SEASON.Get(seasonId);
            if (season == null)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"Season not found for shop purchase: shop_id={product.shop_id}, internal_product_id={product.internal_product_id}, season_id={seasonId}");
            }

            var seasonEndUtcMs = season.end_utc_time?.utcTimeMs ?? 0L;
            if (seasonEndUtcMs <= 0L)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"Season end time is invalid: shop_id={product.shop_id}, internal_product_id={product.internal_product_id}, season_id={seasonId}");
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
                return GameResult.Failure(
                    GAME_ERROR_TYPE.PURCHASE_SEASON_END_SOON_BLOCKED,
                    $"Product purchase is blocked near season end: blockDays={blockedBeforeEndDays}, shop_id={product.shop_id}, internal_product_id={product.internal_product_id}, season_id={seasonId}");
            }

            return GameResult.Ok();
        }

        GameResult<CatalogRefreshState> evaluateCatalogRefreshState(
            ShopCatalogBase catalog,
            bool requireServerTime,
            bool forceCatalogRefresh)
        {
            if (catalog == null || !isValidCatalogType(catalog.CatalogType))
            {
                return GameResult<CatalogRefreshState>.Success(
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
                return GameResult<CatalogRefreshState>.Failure(
                    GAME_ERROR_TYPE.GAME_SERVER_TIME_UNAVAILABLE,
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

            return GameResult<CatalogRefreshState>.Success(
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

        GameResult<CatalogRefreshCycleOutcome> tryRefreshCatalog(
            SHOP_CATALOG_TYPE catalogType,
            bool requireServerTime,
            bool forceCatalogRefresh)
        {
            if (!isValidCatalogType(catalogType))
            {
                return GameResult<CatalogRefreshCycleOutcome>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"Invalid shop catalog_type for refresh: {catalogType}");
            }

            if (!_catalogs.TryGetValue(catalogType, out var catalog) || catalog == null)
            {
                return GameResult<CatalogRefreshCycleOutcome>.Success(
                    new CatalogRefreshCycleOutcome(false, false));
            }

            var evaluated = evaluateCatalogRefreshState(
                catalog,
                requireServerTime,
                forceCatalogRefresh);
            if (evaluated.IsFailure)
                return GameResult<CatalogRefreshCycleOutcome>.Failure(evaluated.Error!);

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
                    if (products[i] is not ShopLimitedProductBase limitedProduct
                        || !isLimitedAdsOrFreeProduct(limitedProduct))
                        continue;

                    limitedProduct.ResetRemainCount();
                    var normalizedShopId = normalizeShopId(limitedProduct.shop_id);
                    if (string.IsNullOrEmpty(normalizedShopId))
                        continue;

                    persistProductRemainState(limitedProduct, normalizedShopId);
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

            return GameResult<CatalogRefreshCycleOutcome>.Success(
                new CatalogRefreshCycleOutcome(didRefreshCatalogProducts, didMutateStorage));
        }

        GameResult<CatalogRefreshCycleOutcome> tryRefreshAllCatalogs(bool requireServerTime)
        {
            var catalogCount = _catalogList.Count;
            if (catalogCount <= 0)
            {
                return GameResult<CatalogRefreshCycleOutcome>.Success(
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
                    return GameResult<CatalogRefreshCycleOutcome>.Failure(refresh.Error!);

                if (refresh.Value.DidRefreshCatalogProducts)
                    didRefreshAnyCatalogProducts = true;
                if (refresh.Value.DidMutateStorage)
                    didMutateAnyStorage = true;
            }

            return GameResult<CatalogRefreshCycleOutcome>.Success(
                new CatalogRefreshCycleOutcome(didRefreshAnyCatalogProducts, didMutateAnyStorage));
        }

        GameResult initializeCore(bool requireServerTime)
        {
            var didInitializeCatalogs = ensureCatalogInitialized();
            if (!_catalogInitialized || _catalogList.Count <= 0)
            {
                _initialized = false;
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
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
            return GameResult.Ok();
        }

        GameResult refreshProductsCore(bool requireServerTime, bool refreshLockState)
        {
            if (!_catalogInitialized || _catalogList.Count <= 0)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "ShopManager.Initialize must be called before refresh.");
            }

            var refresh = tryRefreshAllCatalogs(requireServerTime);
            if (refresh.IsFailure)
                return GameResult.Failure(refresh.Error!);

            var didRefreshCatalogProducts = refresh.Value.DidRefreshCatalogProducts;
            var didMutateStorage = refresh.Value.DidMutateStorage;

            if (didRefreshCatalogProducts)
            {
                synchronizeProductIndexFromCatalogs();
                didMutateStorage = true;
            }

            var syncCatalogRuntime = syncCatalogRuntimeStates(requireServerTime);
            if (syncCatalogRuntime.IsFailure)
                return GameResult.Failure(syncCatalogRuntime.Error!);

            if (syncCatalogRuntime.Value)
                didMutateStorage = true;

            if (didMutateStorage)
                queueRuntimeLocalSave();

            if (refreshLockState)
                refreshCatalogLockState();

            return GameResult.Ok();
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
            _productsByShopId.Clear();
            _limitedShopIdsByCatalog.Clear();

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
            _productsByShopId.Clear();
            _limitedShopIdsByCatalog.Clear();

            for (var i = 0; i < _catalogList.Count; i++)
            {
                var catalog = _catalogList[i];
                if (catalog == null)
                    continue;

                var products = catalog.GetProducts();
                for (var j = 0; j < products.Count; j++)
                {
                    var product = products[j];
                    if (product == null || string.IsNullOrWhiteSpace(product.shop_id))
                        continue;

                    var normalizedShopId = normalizeShopId(product.shop_id);
                    if (string.IsNullOrEmpty(normalizedShopId))
                        continue;

                    if (_productsByShopId.ContainsKey(normalizedShopId))
                    {
                        Debug.LogWarning(
                            $"[{Tag}] Duplicate shop_id across catalogs. Keeping first row: shop_id={normalizedShopId}, catalog={catalog.CatalogType}");
                        continue;
                    }

                    _productsByShopId.Add(normalizedShopId, product);
                    registerLimitedProduct(product, normalizedShopId);
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

        void registerLimitedProduct(ShopProductBase product, string normalizedShopId)
        {
            if (product is not ShopLimitedProductBase limitedProduct
                || !limitedProduct.HasPurchaseLimit
                || string.IsNullOrWhiteSpace(normalizedShopId))
                return;

            if (!_limitedShopIdsByCatalog.TryGetValue(limitedProduct.catalog_type, out var shopIds))
            {
                shopIds = new List<string>();
                _limitedShopIdsByCatalog[limitedProduct.catalog_type] = shopIds;
            }

            shopIds.Add(normalizedShopId);
        }

        void markProductPurchased(ShopLimitedProductBase product)
        {
            if (product == null || !product.HasPurchaseLimit)
                return;

            product.TryConsumeOne();

            var normalizedShopId = normalizeShopId(product.shop_id);
            if (string.IsNullOrEmpty(normalizedShopId))
                return;

            persistProductRemainState(product, normalizedShopId);
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

        void persistProductRemainState(ShopLimitedProductBase product, string normalizedShopId)
        {
            if (product == null || string.IsNullOrWhiteSpace(normalizedShopId))
                return;

            if (product.catalog_type == SHOP_CATALOG_TYPE.DAILY)
            {
                var amount = product is ShopRewardProductBase rewardProduct
                    ? rewardProduct.amount
                    : 1;
                _storage.UpsertDailyCatalogProduct(
                    normalizedShopId,
                    product.DiscountType,
                    product.RemainCount,
                    amount);
                return;
            }

            if (!product.HasPurchaseLimit)
            {
                _storage.RemoveProductRemainCount(product.catalog_type, normalizedShopId);
                return;
            }

            _storage.SetProductRemainCount(product.catalog_type, normalizedShopId, product.RemainCount);
        }

        static bool isLimitedAdsOrFreeProduct(ShopProductBase product)
        {
            return product is ShopLimitedProductBase limitedProduct
                && limitedProduct.HasPurchaseLimit
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

        GameResult<ChestPurchaseState> resolveChestPurchaseRuntime(ShopProductChest product)
        {
            if (product == null)
            {
                return GameResult<ChestPurchaseState>.Failure(
                    GAME_ERROR_TYPE.SHOP_PRODUCT_NOT_FOUND,
                    "Chest product is null.");
            }

            var chestCatalog = GetCatalog<ShopCatalogChest>();
            if (chestCatalog == null)
            {
                return GameResult<ChestPurchaseState>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"Chest catalog is unavailable: shop_id={product.shop_id}");
            }

            var resolveRuntime = chestCatalog.ResolvePurchaseRuntime(product);
            if (resolveRuntime.IsFailure)
                return GameResult<ChestPurchaseState>.Failure(resolveRuntime.Error!);

            return GameResult<ChestPurchaseState>.Success(
                new ChestPurchaseState(chestCatalog, resolveRuntime.Value));
        }

        GameResult<ShopProductBase> checkStandardShopPurchaseCanBuy(
            ShopProductBase product,
            CURRENCY_TYPE currencyType)
        {
            if (product == null)
            {
                return GameResult<ShopProductBase>.Failure(
                    GAME_ERROR_TYPE.SHOP_PRODUCT_NOT_FOUND,
                    "Shop product is null.");
            }

            if (product.ProductType == SHOP_PRODUCT_TYPE.FREE)
                return GameResult<ShopProductBase>.Success(product);

            if (product.ProductType == SHOP_PRODUCT_TYPE.ADS)
            {
                if (tryGetNoAdsRentalRemainingMs(out var noAdsRentalRemainingMs)
                    && noAdsRentalRemainingMs > 0L)
                {
                    return GameResult<ShopProductBase>.Success(product);
                }

                try
                {
                    if (!AdsManager.Instance.CanShow())
                    {
                        return GameResult<ShopProductBase>.Failure(
                            GAME_ERROR_TYPE.SHOP_ADS_NOT_AVAILABLE,
                            $"Shop rewarded ad is not available: shop_id={product.shop_id}");
                    }
                }
                catch (Exception ex)
                {
                    return GameResult<ShopProductBase>.Failure(
                        GAME_ERROR_TYPE.SHOP_ADS_NOT_AVAILABLE,
                        $"Shop rewarded ad check failed: shop_id={product.shop_id}, reason={ex.Message}");
                }

                return GameResult<ShopProductBase>.Success(product);
            }

            if (!tryGetWallet(out var wallet) || wallet == null)
            {
                return GameResult<ShopProductBase>.Failure(
                    GAME_ERROR_TYPE.SHOP_WALLET_UNAVAILABLE,
                    $"Inventory wallet is unavailable: shop_id={product.shop_id}");
            }

            var price = product.Price;
            if (!hasSufficientCurrency(wallet, currencyType, price))
            {
                return GameResult<ShopProductBase>.Failure(
                    GAME_ERROR_TYPE.SHOP_CURRENCY_INSUFFICIENT,
                    $"Insufficient currency for shop purchase: shop_id={product.shop_id}, currency={currencyType}, price={price}, basePrice={product.PriceWithoutDiscount}, discountType={product.DiscountType}");
            }

            return GameResult<ShopProductBase>.Success(product);
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

        GameResult<bool> syncCatalogRuntimeStates(bool requireServerTime)
        {
            var didMutateStorage = false;
            for (var i = 0; i < _catalogList.Count; i++)
            {
                var catalog = _catalogList[i];
                if (catalog == null)
                    continue;

                var sync = catalog.SyncRuntimeState(requireServerTime);
                if (sync.IsFailure)
                    return GameResult<bool>.Failure(sync.Error!);

                if (sync.Value)
                    didMutateStorage = true;
            }

            return GameResult<bool>.Success(didMutateStorage);
        }

        static GameResult<RewardData[]> wrapBuyFailure(string shopId, GameError innerError)
        {
            var normalizedShopId = normalizeShopId(shopId);
            if (innerError.Code == GAME_ERROR_TYPE.SHOP_CURRENCY_INSUFFICIENT)
            {
                return GameResult<RewardData[]>.Failure(
                    GAME_ERROR_TYPE.SHOP_CURRENCY_INSUFFICIENT,
                    $"Insufficient currency for shop purchase: shop_id={normalizedShopId}, inner={innerError.Code}:{innerError.Message}");
            }

            return GameResult<RewardData[]>.Failure(
                GAME_ERROR_TYPE.SHOP_BUY_FAILED,
                $"Shop BuyAsync failed: shop_id={normalizedShopId}, inner={innerError.Code}:{innerError.Message}");
        }

        static bool tryResolvePurchaseProduct(string internalProductId, out PURCHASE purchaseProduct)
        {
            purchaseProduct = null;
            if (string.IsNullOrWhiteSpace(internalProductId))
                return false;

            purchaseProduct = TB_PURCHASE.Get(internalProductId.Trim());
            return purchaseProduct != null;
        }

        GameResult<RewardData[]> applyShopProductRewards(string rewardGroupId, int amount)
        {
            var repeatCount = amount < 1 ? 1 : amount;
            var grantedRewards = new List<RewardData>(repeatCount);

            for (var i = 0; i < repeatCount; i++)
            {
                var apply = RewardManager.Instance.ApplyRewardGroup(rewardGroupId);
                if (apply.IsFailure)
                {
                    rollbackGrantedShopRewards(grantedRewards, rewardGroupId, i);
                    return GameResult<RewardData[]>.Failure(apply.Error!);
                }

                var applied = apply.Value.AppliedRewards;
                if (applied == null || applied.Length == 0)
                    continue;

                grantedRewards.AddRange(applied);
            }

            return GameResult<RewardData[]>.Success(normalizeShopRewards(grantedRewards));
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

        static string normalizeShopId(string shopId)
        {
            return shopId != null ? shopId.Trim() : string.Empty;
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

                GameResult<bool> save;
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
                    || save.Error.Code != GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT)
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
