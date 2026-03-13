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
        const string DefaultAdsAdvertiseId = "advertise_001";
        const long MillisecondsPerDay = 24L * 60L * 60L * 1000L;

        public const string DefaultRentalNoAdsId = "NO_ADS";

        readonly ShopStorage _storage = new();
        readonly Dictionary<SHOP_CATALOG_TYPE, ShopCatalogBase> _catalogs = new();
        readonly List<ShopCatalogBase> _catalogList = new();
        readonly Dictionary<string, ShopProductBase> _productsByShopId = new(StringComparer.Ordinal);
        readonly Dictionary<SHOP_CATALOG_TYPE, List<string>> _limitedShopIdsByCatalog = new();

        bool _catalogInitialized;

        public ShopStorage Storage => _storage;
        public COMMON_ERROR_TYPE LastCanBuyErrorCode { get; private set; } = COMMON_ERROR_TYPE.SUCCESS;
        public string LastCanBuyErrorMessage { get; private set; } = "Success";
        public COMMON_ERROR_TYPE LastCanBuyInnerErrorCode { get; private set; } = COMMON_ERROR_TYPE.SUCCESS;
        public string LastCanBuyInnerErrorMessage { get; private set; } = "Success";

        protected override void onInitAwake()
        {
            var init = initializeCore(requireServerTime: false);
            if (init.IsFailure)
                Debug.LogWarning($"[{Tag}] ShopManager init on awake failed (non-fatal): {init.Error}");
        }

        public CommonResult Initialize()
        {
            return initializeCore(requireServerTime: true);
        }

        public void RebuildCatalogProductsFromStorage()
        {
            rebuildCatalogProducts();
        }

        public IReadOnlyList<ShopCatalogBase> GetCatalogs()
        {
            ensureCatalogInitialized();
            return _catalogList;
        }

        public ShopCatalogBase GetCatalog(SHOP_CATALOG_TYPE catalogType)
        {
            ensureCatalogInitialized();
            if (_catalogs.TryGetValue(catalogType, out var catalog))
                return catalog;

            return ShopCatalogBase.Empty(catalogType);
        }

        public CommonResult ResetAds(SHOP_CATALOG_TYPE catalogType)
        {
            ensureCatalogInitialized();

            if (!isValidCatalogType(catalogType))
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    $"Invalid shop catalogType for ads reset: {catalogType}");
            }

            resetCatalogByType(catalogType);
            if (_catalogs.TryGetValue(catalogType, out var catalog) && catalog != null)
            {
                var intervalMs = getAutoRefreshIntervalMs(catalog.autoRefreshDay);
                if (intervalMs > 0L)
                {
                    var serverNowUtcMs = RemoteDataManager.ServerNowUtcMs;
                    if (serverNowUtcMs > 0L)
                    {
                        _storage.SetAutoRefreshUtcMs(
                            catalogType,
                            getNextRefreshUtcMs(serverNowUtcMs, intervalMs));
                        _storage.SetAdsRefreshUtcMs(
                            catalogType,
                            getNextRefreshUtcMs(serverNowUtcMs, MillisecondsPerDay));
                        catalog.SetRemainRefreshTimeMs(intervalMs);
                    }
                    else
                    {
                        _storage.ClearAutoRefreshUtcMs(catalogType);
                        _storage.ClearAdsRefreshUtcMs(catalogType);
                        catalog.SetRemainRefreshTimeMs(0L);
                    }
                }
                else
                {
                    _storage.ClearAutoRefreshUtcMs(catalogType);
                    _storage.ClearAdsRefreshUtcMs(catalogType);
                    catalog.SetRemainRefreshTimeMs(0L);
                }
            }

            return CommonResult.Ok();
        }

        public CommonResult<long> GetAdsResetRemainingMs(SHOP_CATALOG_TYPE catalogType)
        {
            ensureCatalogInitialized();

            if (!isValidCatalogType(catalogType))
            {
                return CommonResult<long>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    $"Invalid shop catalogType for ads reset remaining: {catalogType}");
            }

            if (!_catalogs.TryGetValue(catalogType, out var catalog) || catalog == null)
                return CommonResult<long>.Success(0L);

            var intervalMs = getAutoRefreshIntervalMs(catalog.autoRefreshDay);
            if (intervalMs <= 0L)
            {
                catalog.SetRemainRefreshTimeMs(0L);
                return CommonResult<long>.Success(0L);
            }

            var serverNowUtcMs = RemoteDataManager.ServerNowUtcMs;
            if (serverNowUtcMs <= 0L)
                return CommonResult<long>.Success(catalog.RemainRefreshTimeMs);

            var nextRefreshUtcMs = _storage.GetAutoRefreshUtcMs(catalogType);
            if (nextRefreshUtcMs <= 0L)
            {
                nextRefreshUtcMs = getNextRefreshUtcMs(serverNowUtcMs, intervalMs);
                _storage.SetAutoRefreshUtcMs(catalogType, nextRefreshUtcMs);
                catalog.SetRemainRefreshTimeMs(intervalMs);
                return CommonResult<long>.Success(intervalMs);
            }

            var remainTimeMs = getRemainingToNextRefreshMs(serverNowUtcMs, nextRefreshUtcMs);
            catalog.SetRemainRefreshTimeMs(remainTimeMs);

            return CommonResult<long>.Success(remainTimeMs);
        }

        public bool CanBuy(string shopId)
        {
            var check = checkCanBuy(shopId);
            if (check.IsFailure)
            {
                var inner = check.Error!;
                LastCanBuyErrorCode = COMMON_ERROR_TYPE.SHOP_CAN_BUY_FAILED;
                LastCanBuyErrorMessage =
                    $"Shop CanBuy failed: shopId={normalizeShopId(shopId)}, inner={inner.Code}:{inner.Message}";
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

        public async Task<CommonResult<RewardData[]>> BuyAsync(string shopId, CancellationToken ct = default)
        {
            var check = checkCanBuy(shopId);
            if (check.IsFailure)
                return wrapBuyFailure(shopId, check.Error!);

            var product = check.Value!;
            CommonResult<RewardData[]> buyResult;
            if (product is ShopProductPurchase purchaseProduct)
                buyResult = await buyPurchaseCatalogAsync(purchaseProduct, ct);
            else if (product is ShopRewardProductBase rewardProduct)
                buyResult = await buyRewardCatalogAsync(rewardProduct, ct);
            else
                buyResult = CommonResult<RewardData[]>.Failure(
                    COMMON_ERROR_TYPE.SHOP_PRODUCT_NOT_FOUND,
                    $"Shop product type is not supported: shopId={product.ShopId}, productType={product.ProductType}");

            if (buyResult.IsFailure)
                return wrapBuyFailure(shopId, buyResult.Error!);

            return buyResult;
        }

        CommonResult<ShopProductBase> checkCanBuy(string shopId)
        {
            var validateProduct = validateShopProductConfig(shopId);
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

            if (product is not ShopRewardProductBase rewardProduct)
            {
                return CommonResult<ShopProductBase>.Failure(
                    COMMON_ERROR_TYPE.SHOP_PRODUCT_NOT_FOUND,
                    $"Shop product type is not supported: shopId={product.ShopId}, productType={product.ProductType}");
            }

            if (rewardProduct.ProductType == SHOP_PRODUCT_TYPE.FREE)
                return CommonResult<ShopProductBase>.Success(product);

            if (rewardProduct.ProductType == SHOP_PRODUCT_TYPE.ADS)
            {
                if (tryGetNoAdsRentalRemainingMs(out var noAdsRentalRemainingMs)
                    && noAdsRentalRemainingMs > 0L)
                {
                    // NO_ADS 대여 상태면 AdsManager.CanShow 체크 없이 구매 가능으로 처리한다.
                    return CommonResult<ShopProductBase>.Success(product);
                }

                try
                {
                    if (!AdsManager.Instance.CanShow(DefaultAdsAdvertiseId))
                    {
                        return CommonResult<ShopProductBase>.Failure(
                            COMMON_ERROR_TYPE.SHOP_ADS_NOT_AVAILABLE,
                            $"Shop rewarded ad is not available: shopId={product.ShopId}, advertiseId={DefaultAdsAdvertiseId}");
                    }
                }
                catch (Exception ex)
                {
                    return CommonResult<ShopProductBase>.Failure(
                        COMMON_ERROR_TYPE.SHOP_ADS_NOT_AVAILABLE,
                        $"Shop rewarded ad check failed: shopId={product.ShopId}, advertiseId={DefaultAdsAdvertiseId}, reason={ex.Message}");
                }

                return CommonResult<ShopProductBase>.Success(product);
            }

            if (!tryGetWallet(out var wallet) || wallet == null)
            {
                return CommonResult<ShopProductBase>.Failure(
                    COMMON_ERROR_TYPE.SHOP_WALLET_UNAVAILABLE,
                    $"Inventory wallet is unavailable: shopId={product.ShopId}");
            }

            var price = rewardProduct.Price;
            if (!hasSufficientCurrency(wallet, rewardProduct.CurrencyType, price))
            {
                return CommonResult<ShopProductBase>.Failure(
                    COMMON_ERROR_TYPE.SHOP_CURRENCY_INSUFFICIENT,
                    $"Insufficient currency for shop purchase: shopId={product.ShopId}, currency={rewardProduct.CurrencyType}, price={price}, basePrice={rewardProduct.PriceWithoutDiscount}, discountType={rewardProduct.DiscountType}");
            }

            return CommonResult<ShopProductBase>.Success(product);
        }

        CommonResult<ShopProductBase> validateShopProductConfig(string shopId)
        {
            ensureCatalogInitialized();

            var resetCatalogRolling = tryResetAllCatalogsByElapsedTime(requireServerTime: true);
            if (resetCatalogRolling.IsFailure)
                return CommonResult<ShopProductBase>.Failure(resetCatalogRolling.Error!);
            if (resetCatalogRolling.Value)
                rebuildCatalogProducts();

            var normalizedShopId = normalizeShopId(shopId);
            if (string.IsNullOrEmpty(normalizedShopId))
            {
                return CommonResult<ShopProductBase>.Failure(
                    COMMON_ERROR_TYPE.SHOP_PRODUCT_ID_EMPTY,
                    "Shop shopId is empty.");
            }

            if (!_productsByShopId.TryGetValue(normalizedShopId, out var product) || product == null)
            {
                return CommonResult<ShopProductBase>.Failure(
                    COMMON_ERROR_TYPE.SHOP_PRODUCT_NOT_FOUND,
                    $"Shop product not found: shopId={normalizedShopId}");
            }

            if (product is ShopRewardProductBase rewardProduct && rewardProduct.PriceWithoutDiscount < 0)
            {
                return CommonResult<ShopProductBase>.Failure(
                    COMMON_ERROR_TYPE.SHOP_PRODUCT_PRICE_INVALID,
                    $"Shop product price is invalid: shopId={product.ShopId}, price={rewardProduct.PriceWithoutDiscount}, discountType={rewardProduct.DiscountType}");
            }

            if (product.HasPurchaseLimit)
            {
                if (product.MaxCount == 0)
                {
                    return CommonResult<ShopProductBase>.Failure(
                        COMMON_ERROR_TYPE.SHOP_PURCHASE_LIMIT_DISABLED,
                        $"Shop purchase is disabled by maxCount=0: shopId={product.ShopId}");
                }

                if (product.RemainCount <= 0)
                {
                    var usedCount = product.MaxCount - product.RemainCount;
                    return CommonResult<ShopProductBase>.Failure(
                        COMMON_ERROR_TYPE.SHOP_PURCHASE_LIMIT_EXCEEDED,
                        $"Shop purchase limit exceeded: shopId={product.ShopId}, maxCount={product.MaxCount}, remainCount={product.RemainCount}, usedCount={usedCount}");
                }
            }

            if (product is ShopProductPurchase purchaseProduct)
            {
                if (string.IsNullOrWhiteSpace(purchaseProduct.InternalProductId))
                {
                    return CommonResult<ShopProductBase>.Failure(
                        COMMON_ERROR_TYPE.PURCHASE_NOT_FOUND,
                        $"Shop purchase internalProductId is empty: shopId={purchaseProduct.ShopId}");
                }

                if (!tryResolvePurchaseProduct(purchaseProduct.InternalProductId, out var purchaseRow) || purchaseRow == null)
                {
                    return CommonResult<ShopProductBase>.Failure(
                        COMMON_ERROR_TYPE.PURCHASE_NOT_FOUND,
                        $"Purchase product not found: shopId={purchaseProduct.ShopId}, internalProductId={purchaseProduct.InternalProductId}");
                }

                if (!purchaseRow.IsActive)
                {
                    return CommonResult<ShopProductBase>.Failure(
                        COMMON_ERROR_TYPE.PURCHASE_NOT_FOUND,
                        $"Purchase product is inactive: shopId={purchaseProduct.ShopId}, internalProductId={purchaseProduct.InternalProductId}");
                }

                return CommonResult<ShopProductBase>.Success(product);
            }

            if (product is not ShopRewardProductBase validateRewardProduct)
            {
                return CommonResult<ShopProductBase>.Failure(
                    COMMON_ERROR_TYPE.SHOP_PRODUCT_NOT_FOUND,
                    $"Shop product type is not supported: shopId={product.ShopId}, productType={product.ProductType}");
            }

            if (validateRewardProduct.ProductType == SHOP_PRODUCT_TYPE.FREE
                || validateRewardProduct.ProductType == SHOP_PRODUCT_TYPE.ADS)
            {
                if (validateRewardProduct.PriceWithoutDiscount != 0)
                {
                    return CommonResult<ShopProductBase>.Failure(
                        COMMON_ERROR_TYPE.SHOP_PRODUCT_PRICE_INVALID,
                        $"Shop pseudo product price must be zero: shopId={validateRewardProduct.ShopId}, productType={validateRewardProduct.ProductType}, price={validateRewardProduct.PriceWithoutDiscount}");
                }
            }

            if (string.IsNullOrWhiteSpace(validateRewardProduct.RewardGroupId))
            {
                return CommonResult<ShopProductBase>.Failure(
                    COMMON_ERROR_TYPE.SHOP_REWARD_GROUP_EMPTY,
                    $"Shop product rewardGroupId is empty: shopId={product.ShopId}");
            }

            return CommonResult<ShopProductBase>.Success(product);
        }

        async Task<CommonResult<RewardData[]>> buyPurchaseCatalogAsync(ShopProductPurchase product, CancellationToken ct)
        {
            var validateSeason = validateSeasonPurchaseWindow(product);
            if (validateSeason.IsFailure)
                return CommonResult<RewardData[]>.Failure(validateSeason.Error!);

            var purchaseResult = await PurchaseManager.Instance.PurchaseAsync(product.InternalProductId, ct);
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
                    AdsManager.Instance.SetDefaultId(DefaultAdsAdvertiseId);
                    var show = await AdsManager.Instance.ShowAsync(ct);
                    if (show.IsFailure)
                    {
                        var details = show.Error != null
                            ? $"inner={show.Error.Code}:{show.Error.Message}"
                            : "inner=unknown";
                        return CommonResult<RewardData[]>.Failure(
                            COMMON_ERROR_TYPE.SHOP_ADS_SHOW_FAILED,
                            $"Shop rewarded ad show failed: shopId={product.ShopId}, advertiseId={DefaultAdsAdvertiseId}, {details}");
                    }
                }
            }
            else if (product.ProductType == SHOP_PRODUCT_TYPE.CURRENCY)
            {
                if (!tryGetWallet(out wallet) || wallet == null)
                {
                    return CommonResult<RewardData[]>.Failure(
                        COMMON_ERROR_TYPE.SHOP_WALLET_UNAVAILABLE,
                        $"Inventory wallet is unavailable: shopId={product.ShopId}");
                }

                var price = product.Price;
                if (price < 0)
                {
                    return CommonResult<RewardData[]>.Failure(
                        COMMON_ERROR_TYPE.SHOP_PRODUCT_PRICE_INVALID,
                        $"Shop product price is invalid: shopId={product.ShopId}, price={price}, basePrice={product.PriceWithoutDiscount}, discountType={product.DiscountType}");
                }

                if (!tryDeductCurrency(wallet, product.CurrencyType, price, out deduction))
                {
                    return CommonResult<RewardData[]>.Failure(
                        COMMON_ERROR_TYPE.SHOP_CURRENCY_INSUFFICIENT,
                        $"Insufficient currency for shop purchase: shopId={product.ShopId}, currency={product.CurrencyType}, price={price}, basePrice={product.PriceWithoutDiscount}, discountType={product.DiscountType}");
                }
            }
            else if (product.ProductType != SHOP_PRODUCT_TYPE.FREE)
            {
                return CommonResult<RewardData[]>.Failure(
                    COMMON_ERROR_TYPE.SHOP_PRODUCT_NOT_FOUND,
                    $"Shop product type is not supported: shopId={product.ShopId}, productType={product.ProductType}");
            }

            var applyRewards = applyShopProductRewards(product.RewardGroupId, product.Amount);
            if (applyRewards.IsFailure)
            {
                if (deduction.HasDeduction)
                    rollbackCurrency(wallet, deduction);

                var details = applyRewards.Error != null
                    ? $"inner={applyRewards.Error.Code}:{applyRewards.Error.Message}"
                    : "inner=unknown";
                return CommonResult<RewardData[]>.Failure(
                    COMMON_ERROR_TYPE.SHOP_REWARD_APPLY_FAILED,
                    $"Shop reward apply failed: shopId={product.ShopId}, rewardGroupId={product.RewardGroupId}, {details}");
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
            if (!tryResolvePurchaseProduct(product.InternalProductId, out var purchaseProduct) || purchaseProduct == null)
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.PURCHASE_NOT_FOUND,
                    $"Purchase product not found: shopId={product.ShopId}, internalProductId={product.InternalProductId}");
            }

            var seasonId = (product.SeasonId ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(seasonId))
                return CommonResult.Ok();

            var season = TB_SEASON.Get(seasonId);
            if (season == null)
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    $"Season not found for shop purchase: shopId={product.ShopId}, internalProductId={product.InternalProductId}, seasonId={seasonId}");
            }

            var seasonEndUtcMs = season.EndUtcTime?.utcTimeMs ?? 0L;
            if (seasonEndUtcMs <= 0L)
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    $"Season end time is invalid: shopId={product.ShopId}, internalProductId={product.InternalProductId}, seasonId={seasonId}");
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
                    $"Product purchase is blocked near season end: blockDays={blockedBeforeEndDays}, shopId={product.ShopId}, internalProductId={product.InternalProductId}, seasonId={seasonId}");
            }

            return CommonResult.Ok();
        }

        CommonResult<bool> tryCatalogResetByElapsedTime(SHOP_CATALOG_TYPE catalogType, bool requireServerTime)
        {
            if (!isValidCatalogType(catalogType))
            {
                return CommonResult<bool>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    $"Invalid shop catalogType for ads reset: {catalogType}");
            }

            if (!_catalogs.TryGetValue(catalogType, out var catalog) || catalog == null)
                return CommonResult<bool>.Success(false);

            var didResetCatalog = false;
            var intervalMs = getAutoRefreshIntervalMs(catalog.autoRefreshDay);
            if (intervalMs <= 0L)
            {
                catalog.SetRemainRefreshTimeMs(0L);
                _storage.ClearAutoRefreshUtcMs(catalogType);
            }
            else
            {
                var serverNowUtcMs = RemoteDataManager.ServerNowUtcMs;
                if (serverNowUtcMs <= 0L)
                {
                    if (requireServerTime)
                    {
                        return CommonResult<bool>.Failure(
                            COMMON_ERROR_TYPE.SHOP_SERVER_TIME_UNAVAILABLE,
                            "Server time is invalid.");
                    }
                }
                else
                {
                    var nextRefreshUtcMs = _storage.GetAutoRefreshUtcMs(catalogType);
                    if (nextRefreshUtcMs <= 0L)
                    {
                        _storage.SetAutoRefreshUtcMs(
                            catalogType,
                            getNextRefreshUtcMs(serverNowUtcMs, intervalMs));
                        catalog.SetRemainRefreshTimeMs(intervalMs);
                    }
                    else
                    {
                        var remainTimeMs = getRemainingToNextRefreshMs(serverNowUtcMs, nextRefreshUtcMs);
                        if (remainTimeMs <= 0L)
                        {
                            resetCatalogByType(catalogType);
                            _storage.SetAutoRefreshUtcMs(
                                catalogType,
                                getNextRefreshUtcMs(serverNowUtcMs, intervalMs));
                            _storage.SetAdsRefreshUtcMs(
                                catalogType,
                                getNextRefreshUtcMs(serverNowUtcMs, MillisecondsPerDay));
                            catalog.SetRemainRefreshTimeMs(intervalMs);
                            didResetCatalog = true;
                        }
                        else
                        {
                            catalog.SetRemainRefreshTimeMs(remainTimeMs);
                        }
                    }
                }
            }

            if (didResetCatalog)
                return CommonResult<bool>.Success(true);

            var adsRefill = tryRefillAdsFreeProductsByCatalog(catalogType, requireServerTime);
            if (adsRefill.IsFailure)
                return CommonResult<bool>.Failure(adsRefill.Error!);

            return CommonResult<bool>.Success(didResetCatalog);
        }

        CommonResult<bool> tryRefillAdsFreeProductsByCatalog(SHOP_CATALOG_TYPE catalogType, bool requireServerTime)
        {
            if (!_catalogs.TryGetValue(catalogType, out var catalog) || catalog == null)
                return CommonResult<bool>.Success(false);

            var products = catalog.GetProducts();
            var hasLimitedAdsOrFreeProduct = false;
            for (var i = 0; i < products.Count; i++)
            {
                if (isLimitedAdsOrFreeProduct(products[i]))
                {
                    hasLimitedAdsOrFreeProduct = true;
                    break;
                }
            }

            if (!hasLimitedAdsOrFreeProduct)
            {
                _storage.ClearAdsRefreshUtcMs(catalogType);
                return CommonResult<bool>.Success(false);
            }

            var serverNowUtcMs = RemoteDataManager.ServerNowUtcMs;
            if (serverNowUtcMs <= 0L)
            {
                if (requireServerTime)
                {
                    return CommonResult<bool>.Failure(
                        COMMON_ERROR_TYPE.SHOP_SERVER_TIME_UNAVAILABLE,
                        "Server time is invalid.");
                }

                return CommonResult<bool>.Success(false);
            }

            var nextAdsRefreshUtcMs = _storage.GetAdsRefreshUtcMs(catalogType);
            if (nextAdsRefreshUtcMs > serverNowUtcMs)
                return CommonResult<bool>.Success(false);

            var didRefill = false;
            for (var i = 0; i < products.Count; i++)
            {
                var product = products[i];
                if (!isLimitedAdsOrFreeProduct(product))
                    continue;

                product.ResetRemainCount();
                var normalizedShopId = normalizeShopId(product.ShopId);
                if (string.IsNullOrEmpty(normalizedShopId))
                    continue;

                persistProductRemainState(product, normalizedShopId);
                didRefill = true;
            }

            _storage.SetAdsRefreshUtcMs(
                catalogType,
                getNextRefreshUtcMs(serverNowUtcMs, MillisecondsPerDay));
            return CommonResult<bool>.Success(didRefill);
        }

        CommonResult<bool> tryResetAllCatalogsByElapsedTime(bool requireServerTime)
        {
            ensureCatalogInitialized();

            var catalogCount = _catalogList.Count;
            if (catalogCount <= 0)
                return CommonResult<bool>.Success(false);

            var catalogTypes = new SHOP_CATALOG_TYPE[catalogCount];
            for (var i = 0; i < catalogCount; i++)
                catalogTypes[i] = _catalogList[i].CatalogType;

            var didResetAnyCatalog = false;
            for (var i = 0; i < catalogTypes.Length; i++)
            {
                var catalogType = catalogTypes[i];
                if (!isValidCatalogType(catalogType))
                    continue;

                var reset = tryCatalogResetByElapsedTime(catalogType, requireServerTime);
                if (reset.IsFailure)
                    return reset;
                if (reset.Value)
                    didResetAnyCatalog = true;
            }

            return CommonResult<bool>.Success(didResetAnyCatalog);
        }

        CommonResult initializeCore(bool requireServerTime)
        {
            ensureCatalogInitialized();

            var shouldRebuildOnInitialize = shouldRebuildCatalogProductsOnInitialize();

            var resetCatalogRolling = tryResetAllCatalogsByElapsedTime(requireServerTime);
            if (resetCatalogRolling.IsFailure)
                return CommonResult.Failure(resetCatalogRolling.Error!);

            if (shouldRebuildOnInitialize || resetCatalogRolling.Value)
                rebuildCatalogProducts();

            return CommonResult.Ok();
        }

        void resetCatalogByType(SHOP_CATALOG_TYPE catalogType)
        {
            if (_limitedShopIdsByCatalog.TryGetValue(catalogType, out var shopIds))
            {
                _storage.ClearProductRemainCounts(catalogType, shopIds);
                if (catalogType != SHOP_CATALOG_TYPE.DAILY)
                    resetProductRemainCounts(catalogType, shopIds);
            }

            if (catalogType == SHOP_CATALOG_TYPE.DAILY)
                _storage.ClearDailyCatalogProducts();
        }

        void ensureCatalogInitialized()
        {
            if (_catalogInitialized && _productsByShopId.Count > 0)
                return;

            rebuildCatalogProducts();
        }

        void rebuildCatalogProducts()
        {
            _catalogs.Clear();
            _catalogList.Clear();
            _productsByShopId.Clear();
            _limitedShopIdsByCatalog.Clear();

            var sourceCatalogs = ShopCatalogBase.CreateDefaultCatalogs(_storage);
            for (var i = 0; i < sourceCatalogs.Count; i++)
                addCatalog(sourceCatalogs[i]);

            _catalogInitialized = true;
        }

        void addCatalog(ShopCatalogBase sourceCatalog)
        {
            if (sourceCatalog == null)
                return;

            sourceCatalog.Initialize();
            var catalogType = sourceCatalog.CatalogType;
            var sourceProducts = sourceCatalog.GetProducts();
            var products = new List<ShopProductBase>(sourceProducts.Count);
            for (var i = 0; i < sourceProducts.Count; i++)
            {
                var product = sourceProducts[i];
                if (product == null || string.IsNullOrWhiteSpace(product.ShopId))
                    continue;

                var normalizedShopId = product.ShopId.Trim();
                if (_productsByShopId.ContainsKey(normalizedShopId))
                {
                    Debug.LogWarning(
                        $"[{Tag}] Duplicate shopId across catalogs. Keeping first row: shopId={normalizedShopId}, catalog={catalogType}");
                    continue;
                }

                syncProductRemainState(product, normalizedShopId);
                _productsByShopId.Add(normalizedShopId, product);
                products.Add(product);
                registerLimitedProduct(product, normalizedShopId);
            }

            var normalizedCatalog = ShopCatalogBase.Create(catalogType, products);
            _catalogs[catalogType] = normalizedCatalog;
            _catalogList.Add(normalizedCatalog);
        }

        void registerLimitedProduct(ShopProductBase product, string normalizedShopId)
        {
            if (product == null || !product.HasPurchaseLimit || string.IsNullOrWhiteSpace(normalizedShopId))
                return;

            if (!_limitedShopIdsByCatalog.TryGetValue(product.CatalogType, out var shopIds))
            {
                shopIds = new List<string>();
                _limitedShopIdsByCatalog[product.CatalogType] = shopIds;
            }

            shopIds.Add(normalizedShopId);
        }

        void markProductPurchased(ShopProductBase product)
        {
            if (product == null || !product.HasPurchaseLimit)
                return;

            product.TryConsumeOne();

            var normalizedShopId = normalizeShopId(product.ShopId);
            if (string.IsNullOrEmpty(normalizedShopId))
                return;

            persistProductRemainState(product, normalizedShopId);
        }

        void markAdsRefreshOnPurchaseIfNeeded(ShopRewardProductBase product)
        {
            if (product == null)
                return;

            if (product.ProductType != SHOP_PRODUCT_TYPE.ADS
                && product.ProductType != SHOP_PRODUCT_TYPE.FREE)
            {
                return;
            }

            if (!isValidCatalogType(product.CatalogType))
                return;

            var serverNowUtcMs = RemoteDataManager.ServerNowUtcMs;
            if (serverNowUtcMs <= 0L)
                return;

            _storage.SetAdsRefreshUtcMs(
                product.CatalogType,
                getNextRefreshUtcMs(serverNowUtcMs, MillisecondsPerDay));
        }

        void persistProductRemainState(ShopProductBase product, string normalizedShopId)
        {
            if (product == null || string.IsNullOrWhiteSpace(normalizedShopId))
                return;

            if (product.CatalogType == SHOP_CATALOG_TYPE.DAILY)
            {
                if (isDailyStoredProduct(product))
                {
                    _storage.RemoveProductRemainCount(product.CatalogType, normalizedShopId);
                    _storage.UpsertDailyCatalogProduct(
                        normalizedShopId,
                        product.DiscountType,
                        product.RemainCount);
                }
                else
                {
                    _storage.RemoveDailyCatalogProduct(normalizedShopId);
                    if (product.HasPurchaseLimit)
                        _storage.SetProductRemainCount(product.CatalogType, normalizedShopId, product.RemainCount);
                    else
                        _storage.RemoveProductRemainCount(product.CatalogType, normalizedShopId);
                }

                return;
            }

            if (!product.HasPurchaseLimit)
            {
                _storage.RemoveProductRemainCount(product.CatalogType, normalizedShopId);
                return;
            }

            _storage.SetProductRemainCount(product.CatalogType, normalizedShopId, product.RemainCount);
        }

        void syncProductRemainState(ShopProductBase product, string normalizedShopId)
        {
            if (product == null || string.IsNullOrWhiteSpace(normalizedShopId))
                return;

            if (!product.HasPurchaseLimit)
            {
                product.SetRemainCount(-1);
                _storage.RemoveProductRemainCount(product.CatalogType, normalizedShopId);
                if (product.CatalogType == SHOP_CATALOG_TYPE.DAILY)
                {
                    if (isDailyStoredProduct(product))
                    {
                        _storage.UpsertDailyCatalogProduct(
                            normalizedShopId,
                            product.DiscountType,
                            product.RemainCount);
                    }
                    else
                    {
                        _storage.RemoveDailyCatalogProduct(normalizedShopId);
                    }
                }

                return;
            }

            if (isDailyStoredProduct(product)
                && _storage.TryGetDailyCatalogProduct(normalizedShopId, out var dailyState)
                && dailyState != null)
            {
                product.SetRemainCount(dailyState.remainCount);
                _storage.RemoveProductRemainCount(product.CatalogType, normalizedShopId);
                _storage.UpsertDailyCatalogProduct(
                    normalizedShopId,
                    product.DiscountType,
                    product.RemainCount);
                return;
            }

            if (_storage.TryGetProductRemainCount(product.CatalogType, normalizedShopId, out var storedRemainCount))
            {
                product.SetRemainCount(storedRemainCount);
            }
            else if (_storage.TryTakeLegacyPurchaseCount(normalizedShopId, out var legacyPurchaseCount))
            {
                product.SetRemainCount(product.MaxCount - legacyPurchaseCount);
            }
            else
            {
                product.ResetRemainCount();
            }

            if (product.CatalogType == SHOP_CATALOG_TYPE.DAILY)
            {
                if (isDailyStoredProduct(product))
                {
                    _storage.RemoveProductRemainCount(product.CatalogType, normalizedShopId);
                    _storage.UpsertDailyCatalogProduct(
                        normalizedShopId,
                        product.DiscountType,
                        product.RemainCount);
                }
                else
                {
                    _storage.RemoveDailyCatalogProduct(normalizedShopId);
                    _storage.SetProductRemainCount(product.CatalogType, normalizedShopId, product.RemainCount);
                }

                return;
            }

            _storage.SetProductRemainCount(product.CatalogType, normalizedShopId, product.RemainCount);
        }

        void resetProductRemainCounts(SHOP_CATALOG_TYPE catalogType, IReadOnlyList<string> shopIds)
        {
            if (shopIds == null || shopIds.Count <= 0)
                return;

            for (var i = 0; i < shopIds.Count; i++)
            {
                var normalizedShopId = normalizeShopId(shopIds[i]);
                if (string.IsNullOrEmpty(normalizedShopId))
                    continue;

                if (!_productsByShopId.TryGetValue(normalizedShopId, out var product)
                    || product == null
                    || !product.HasPurchaseLimit)
                {
                    continue;
                }

                product.ResetRemainCount();
                if (product.CatalogType == SHOP_CATALOG_TYPE.DAILY)
                {
                    if (isDailyStoredProduct(product))
                    {
                        _storage.RemoveProductRemainCount(product.CatalogType, normalizedShopId);
                        _storage.UpsertDailyCatalogProduct(
                            normalizedShopId,
                            product.DiscountType,
                            product.RemainCount);
                    }
                    else
                    {
                        _storage.RemoveDailyCatalogProduct(normalizedShopId);
                        _storage.SetProductRemainCount(product.CatalogType, normalizedShopId, product.RemainCount);
                    }

                    continue;
                }

                _storage.SetProductRemainCount(
                    catalogType != SHOP_CATALOG_TYPE.NONE ? catalogType : product.CatalogType,
                    normalizedShopId,
                    product.RemainCount);
            }
        }

        static bool isLimitedAdsOrFreeProduct(ShopProductBase product)
        {
            if (product is not ShopRewardProductBase rewardProduct)
                return false;

            if (!product.HasPurchaseLimit)
                return false;

            return rewardProduct.ProductType == SHOP_PRODUCT_TYPE.ADS
                || rewardProduct.ProductType == SHOP_PRODUCT_TYPE.FREE;
        }

        static bool isDailyStoredProduct(ShopProductBase product)
        {
            if (product == null || product.CatalogType != SHOP_CATALOG_TYPE.DAILY)
                return false;

            if (product is not ShopRewardProductBase rewardProduct)
                return true;

            return rewardProduct.ProductType != SHOP_PRODUCT_TYPE.ADS
                && rewardProduct.ProductType != SHOP_PRODUCT_TYPE.FREE;
        }

        static bool isValidCatalogType(SHOP_CATALOG_TYPE catalogType)
        {
            return catalogType != SHOP_CATALOG_TYPE.NONE;
        }

        bool shouldRebuildCatalogProductsOnInitialize()
        {
            if (!_catalogInitialized)
                return true;

            if (_catalogList.Count <= 0 || _productsByShopId.Count <= 0)
                return true;

            if (!_catalogs.ContainsKey(SHOP_CATALOG_TYPE.DAILY)
                || !_catalogs.ContainsKey(SHOP_CATALOG_TYPE.CHEST)
                || !_catalogs.ContainsKey(SHOP_CATALOG_TYPE.PURCHASE)
                || !_catalogs.ContainsKey(SHOP_CATALOG_TYPE.GOLD))
            {
                return true;
            }

            var dailyStorage = _storage.GetDailyCatalogProducts();
            if ((dailyStorage == null || dailyStorage.Count != 5)
                && hasDailyStoredProductRows())
            {
                return true;
            }

            return false;
        }

        static bool hasDailyStoredProductRows()
        {
            var rows = TB_SHOP_DAILY.GetAll();
            if (rows == null || rows.Count <= 0)
                return false;

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null)
                    continue;

                if (row.CurrencyType == CURRENCY_TYPE.ADS
                    || row.CurrencyType == CURRENCY_TYPE.FREE)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        static long getAutoRefreshIntervalMs(int autoRefreshDay)
        {
            if (autoRefreshDay <= 0)
                return 0L;

            return MillisecondsPerDay * autoRefreshDay;
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

        static CommonResult<RewardData[]> wrapBuyFailure(string shopId, CommonError innerError)
        {
            var normalizedShopId = normalizeShopId(shopId);
            if (innerError.Code == COMMON_ERROR_TYPE.SHOP_CURRENCY_INSUFFICIENT)
            {
                return CommonResult<RewardData[]>.Failure(
                    COMMON_ERROR_TYPE.SHOP_CURRENCY_INSUFFICIENT,
                    $"Insufficient currency for shop purchase: shopId={normalizedShopId}, inner={innerError.Code}:{innerError.Message}");
            }

            return CommonResult<RewardData[]>.Failure(
                COMMON_ERROR_TYPE.SHOP_BUY_FAILED,
                $"Shop BuyAsync failed: shopId={normalizedShopId}, inner={innerError.Code}:{innerError.Message}");
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
                var rollback = InventoryManager.Instance.RevokeRewards(grantedRewards.ToArray());
                if (rollback.IsFailure)
                {
                    Debug.LogError(
                        $"[{Tag}] Shop reward rollback failed after apply failure: rewardGroupId={rewardGroupId}, appliedLoopCount={appliedLoopCount}, grantedRewardCount={grantedRewards.Count}, reason={rollback.Error}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[{Tag}] Shop reward rollback threw exception: rewardGroupId={rewardGroupId}, appliedLoopCount={appliedLoopCount}, grantedRewardCount={grantedRewards.Count}, exception={ex.Message}");
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
