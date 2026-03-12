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
        readonly Dictionary<SHOP_CATALOG_TYPE, ShopCatalog> _catalogs = new();
        readonly List<ShopCatalog> _catalogList = new();
        readonly Dictionary<string, ShopProductBase> _productsByShopId = new(StringComparer.Ordinal);

        bool _catalogInitialized;

        public ShopStorage Storage => _storage;
        public COMMON_ERROR_TYPE LastCanBuyErrorCode { get; private set; } = COMMON_ERROR_TYPE.SUCCESS;
        public string LastCanBuyErrorMessage { get; private set; } = "Success";
        public COMMON_ERROR_TYPE LastCanBuyInnerErrorCode { get; private set; } = COMMON_ERROR_TYPE.SUCCESS;
        public string LastCanBuyInnerErrorMessage { get; private set; } = "Success";

        protected override void onInitAwake()
        {
            rebuildCatalogProducts();
            tryDailyReset(requireServerTime: false);
        }

        public IReadOnlyList<ShopCatalog> GetCatalogs()
        {
            ensureCatalogInitialized();
            return _catalogList;
        }

        public ShopCatalog GetCatalog(SHOP_CATALOG_TYPE catalogType)
        {
            ensureCatalogInitialized();
            if (_catalogs.TryGetValue(catalogType, out var catalog))
                return catalog;

            return ShopCatalog.Empty(catalogType);
        }

        public IReadOnlyList<ShopProductBase> GetProducts(SHOP_CATALOG_TYPE catalogType)
        {
            return GetCatalog(catalogType).Products;
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

            if (!hasSufficientCurrency(wallet, rewardProduct.CurrencyType, rewardProduct.Price))
            {
                return CommonResult<ShopProductBase>.Failure(
                    COMMON_ERROR_TYPE.SHOP_CURRENCY_INSUFFICIENT,
                    $"Insufficient currency for shop purchase: shopId={product.ShopId}, currency={rewardProduct.CurrencyType}, price={rewardProduct.Price}");
            }

            return CommonResult<ShopProductBase>.Success(product);
        }

        CommonResult<ShopProductBase> validateShopProductConfig(string shopId)
        {
            ensureCatalogInitialized();

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

            var ensureReset = tryDailyReset(requireServerTime: product.HasPurchaseLimit);
            if (ensureReset.IsFailure)
                return CommonResult<ShopProductBase>.Failure(ensureReset.Error!);

            if (product is ShopRewardProductBase rewardProduct && rewardProduct.Price < 0)
            {
                return CommonResult<ShopProductBase>.Failure(
                    COMMON_ERROR_TYPE.SHOP_PRODUCT_PRICE_INVALID,
                    $"Shop product price is invalid: shopId={product.ShopId}, price={rewardProduct.Price}");
            }

            if (product.HasPurchaseLimit)
            {
                if (product.MaxCount == 0)
                {
                    return CommonResult<ShopProductBase>.Failure(
                        COMMON_ERROR_TYPE.SHOP_PURCHASE_LIMIT_DISABLED,
                        $"Shop purchase is disabled by maxCount=0: shopId={product.ShopId}");
                }

                var purchaseCount = _storage.GetPurchaseCount(product.ShopId);
                if (purchaseCount >= product.MaxCount)
                {
                    return CommonResult<ShopProductBase>.Failure(
                        COMMON_ERROR_TYPE.SHOP_PURCHASE_LIMIT_EXCEEDED,
                        $"Shop purchase limit exceeded: shopId={product.ShopId}, maxCount={product.MaxCount}, purchaseCount={purchaseCount}");
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
                if (validateRewardProduct.Price != 0)
                {
                    return CommonResult<ShopProductBase>.Failure(
                        COMMON_ERROR_TYPE.SHOP_PRODUCT_PRICE_INVALID,
                        $"Shop pseudo product price must be zero: shopId={validateRewardProduct.ShopId}, productType={validateRewardProduct.ProductType}, price={validateRewardProduct.Price}");
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
                _storage.IncrementPurchaseCount(product.ShopId);

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

                if (!tryDeductCurrency(wallet, product.CurrencyType, product.Price, out deduction))
                {
                    return CommonResult<RewardData[]>.Failure(
                        COMMON_ERROR_TYPE.SHOP_CURRENCY_INSUFFICIENT,
                        $"Insufficient currency for shop purchase: shopId={product.ShopId}, currency={product.CurrencyType}, price={product.Price}");
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
                _storage.IncrementPurchaseCount(product.ShopId);

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

            var seasonId = !string.IsNullOrWhiteSpace(product.SeasonId)
                ? product.SeasonId.Trim()
                : (purchaseProduct.SeasonId ?? string.Empty).Trim();

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

            if (!tryGetServerNowUtcMs(out var serverNowUtcMs))
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.SHOP_SERVER_TIME_UNAVAILABLE,
                    "Server time is unavailable. Initialize RemoteConfigManager before shop purchase.");
            }

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

        CommonResult tryDailyReset(bool requireServerTime)
        {
            if (!tryGetServerNowUtcMs(out var serverNowUtcMs))
            {
                if (requireServerTime)
                {
                    return CommonResult.Failure(
                        COMMON_ERROR_TYPE.SHOP_SERVER_TIME_UNAVAILABLE,
                        "Server time is unavailable. Initialize RemoteConfigManager before shop purchase.");
                }

                return CommonResult.Ok();
            }

            var serverDayStartUtcMs = toUtcDayStart(serverNowUtcMs);
            if (serverDayStartUtcMs <= 0L)
            {
                if (requireServerTime)
                {
                    return CommonResult.Failure(
                        COMMON_ERROR_TYPE.SHOP_SERVER_TIME_UNAVAILABLE,
                        "Server time is invalid. Initialize RemoteConfigManager before shop purchase.");
                }

                return CommonResult.Ok();
            }

            if (_storage.lastResetUtcDayStartMs != serverDayStartUtcMs)
            {
                _storage.ResetDaily(serverDayStartUtcMs);
                rebuildCatalogProducts();
            }

            return CommonResult.Ok();
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

            addCatalogProducts(SHOP_CATALOG_TYPE.DAILY);
            addCatalogProducts(SHOP_CATALOG_TYPE.CHEST);
            addCatalogProducts(SHOP_CATALOG_TYPE.PURCHASE);
            addCatalogProducts(SHOP_CATALOG_TYPE.GOLD);

            _catalogInitialized = true;
        }

        void addCatalogProducts(SHOP_CATALOG_TYPE catalogType)
        {
            var sourceCatalog = ShopProductFactory.BuildCatalog(catalogType);
            var sourceProducts = sourceCatalog.Products;
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

                _productsByShopId.Add(normalizedShopId, product);
                products.Add(product);
            }

            var normalizedCatalog = new ShopCatalog(catalogType, products);
            _catalogs[catalogType] = normalizedCatalog;
            _catalogList.Add(normalizedCatalog);
        }

        static CommonResult<RewardData[]> wrapBuyFailure(string shopId, CommonError innerError)
        {
            var normalizedShopId = normalizeShopId(shopId);
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

        static bool tryGetServerNowUtcMs(out long serverNowUtcMs)
        {
            serverNowUtcMs = 0L;
            if (!RemoteConfigManager.TryGet(out var remoteConfigManager)
                || remoteConfigManager == null)
            {
                return false;
            }

            return remoteConfigManager.TryGetServerNowUtcMs(out serverNowUtcMs);
        }

        static long toUtcDayStart(long utcMs)
        {
            if (utcMs <= 0L)
                return 0L;

            return utcMs - (utcMs % MillisecondsPerDay);
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
