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

        public ShopStorage Storage => _storage;
        public COMMON_ERROR_TYPE LastCanBuyErrorCode { get; private set; } = COMMON_ERROR_TYPE.SUCCESS;
        public string LastCanBuyErrorMessage { get; private set; } = "Success";

        public bool CanBuy(string productId)
        {
            var check = checkCanBuy(productId);
            if (check.IsFailure)
            {
                LastCanBuyErrorCode = check.Error!.Code;
                LastCanBuyErrorMessage = check.Error.Message;
            }
            else
            {
                LastCanBuyErrorCode = COMMON_ERROR_TYPE.SUCCESS;
                LastCanBuyErrorMessage = "Success";
            }
            return check.IsSuccess;
        }

        public async Task<CommonResult<RewardData[]>> BuyAsync(string productId, CancellationToken ct = default)
        {
            var validateProduct = validateProductConfig(productId, out var product);
            if (validateProduct.IsFailure)
                return CommonResult<RewardData[]>.Failure(validateProduct.Error!);

            var prepareLimit = preparePurchaseLimitForBuy(product, out var limitState);
            if (prepareLimit.IsFailure)
                return CommonResult<RewardData[]>.Failure(prepareLimit.Error!);

            var usePurchaseLimit = limitState != null;

            var wallet = default(InventoryWallet);
            var deduction = default(CurrencyDeduction);
            if (product.CurrencyType == CURRENCY_TYPE.ADS)
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
                            $"Shop rewarded ad show failed: productId={product.ProductId}, advertiseId={DefaultAdsAdvertiseId}, {details}");
                    }
                }
            }
            else
            {
                if (!tryGetWallet(out wallet) || wallet == null)
                {
                    return CommonResult<RewardData[]>.Failure(
                        COMMON_ERROR_TYPE.SHOP_WALLET_UNAVAILABLE,
                        $"Inventory wallet is unavailable: productId={product.ProductId}");
                }

                if (!tryDeductCurrency(wallet, product.CurrencyType, product.Price, out deduction))
                {
                    return CommonResult<RewardData[]>.Failure(
                        COMMON_ERROR_TYPE.SHOP_CURRENCY_INSUFFICIENT,
                        $"Insufficient currency for shop purchase: productId={product.ProductId}, currency={product.CurrencyType}, price={product.Price}");
                }
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
                    $"Shop reward apply failed: productId={product.ProductId}, rewardGroupId={product.RewardGroupId}, {details}");
            }

            if (usePurchaseLimit && limitState != null)
                limitState.purchaseCount++;

            var save = await SaveDataManager.Instance.SaveGameStorageAsync(true, ct);
            if (save.IsFailure)
                Debug.LogWarning($"[{Tag}] Post-shop purchase save failed (non-fatal): {save.Error}");

            var applied = applyRewards.Value ?? Array.Empty<RewardData>();
            return CommonResult<RewardData[]>.Success(applied);
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

        static bool isPurchaseLimitEnabled(ShopProduct product)
        {
            return product.MaxCount >= 0 && product.ResetDays >= 0;
        }

        CommonResult checkCanBuy(string productId)
        {
            var validateProduct = validateProductConfig(productId, out var product);
            if (validateProduct.IsFailure)
                return validateProduct;

            var validateLimit = validatePurchaseLimitForCanBuy(product);
            if (validateLimit.IsFailure)
                return validateLimit;

            if (product.CurrencyType == CURRENCY_TYPE.ADS)
            {
                if (tryGetNoAdsRentalRemainingMs(out var noAdsRentalRemainingMs)
                    && noAdsRentalRemainingMs > 0L)
                {
                    // NO_ADS 대여 상태면 AdsManager.CanShow 체크 없이 구매 가능으로 처리한다.
                    return CommonResult.Ok();
                }

                try
                {
                    if (!AdsManager.Instance.CanShow(DefaultAdsAdvertiseId))
                    {
                        return CommonResult.Failure(
                            COMMON_ERROR_TYPE.SHOP_ADS_NOT_AVAILABLE,
                            $"Shop rewarded ad is not available: productId={product.ProductId}, advertiseId={DefaultAdsAdvertiseId}");
                    }
                }
                catch (Exception ex)
                {
                    return CommonResult.Failure(
                        COMMON_ERROR_TYPE.SHOP_ADS_NOT_AVAILABLE,
                        $"Shop rewarded ad check failed: productId={product.ProductId}, advertiseId={DefaultAdsAdvertiseId}, reason={ex.Message}");
                }

                return CommonResult.Ok();
            }

            if (!tryGetWallet(out var wallet) || wallet == null)
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.SHOP_WALLET_UNAVAILABLE,
                    $"Inventory wallet is unavailable: productId={product.ProductId}");
            }

            if (!hasSufficientCurrency(wallet, product.CurrencyType, product.Price))
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.SHOP_CURRENCY_INSUFFICIENT,
                    $"Insufficient currency for shop purchase: productId={product.ProductId}, currency={product.CurrencyType}, price={product.Price}");
            }

            return CommonResult.Ok();
        }

        CommonResult validatePurchaseLimitForCanBuy(ShopProduct product)
        {
            if (!isPurchaseLimitEnabled(product))
                return CommonResult.Ok();

            if (product.MaxCount == 0)
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.SHOP_PURCHASE_LIMIT_DISABLED,
                    $"Shop purchase is disabled by maxCount=0: productId={product.ProductId}");
            }

            if (product.ResetDays <= 0)
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.SHOP_PURCHASE_LIMIT_RESET_DAYS_INVALID,
                    $"Shop product resetDays must be >= 1 when purchase limit is enabled: productId={product.ProductId}, resetDays={product.ResetDays}");
            }

            if (!tryGetServerNowUtcMs(out var serverNowUtcMs))
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.SHOP_SERVER_TIME_UNAVAILABLE,
                    "Server time is unavailable. Initialize RemoteConfigManager before shop purchase.");
            }

            if (!hasRemainingPurchaseLimit(product, serverNowUtcMs))
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.SHOP_PURCHASE_LIMIT_EXCEEDED,
                    $"Shop purchase limit exceeded: productId={product.ProductId}, maxCount={product.MaxCount}, resetDays={product.ResetDays}");
            }

            return CommonResult.Ok();
        }

        CommonResult preparePurchaseLimitForBuy(ShopProduct product, out ShopPurchaseLimitState limitState)
        {
            limitState = null;
            if (!isPurchaseLimitEnabled(product))
                return CommonResult.Ok();

            if (product.MaxCount == 0)
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.SHOP_PURCHASE_LIMIT_DISABLED,
                    $"Shop purchase is disabled by maxCount=0: productId={product.ProductId}");
            }

            if (product.ResetDays <= 0)
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.SHOP_PURCHASE_LIMIT_RESET_DAYS_INVALID,
                    $"Shop product resetDays must be >= 1 when purchase limit is enabled: productId={product.ProductId}, resetDays={product.ResetDays}");
            }

            if (!tryGetServerNowUtcMs(out var serverNowUtcMs))
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.SHOP_SERVER_TIME_UNAVAILABLE,
                    "Server time is unavailable. Initialize RemoteConfigManager before shop purchase.");
            }

            limitState = _storage.GetOrCreatePurchaseLimit(product.ProductId);
            if (limitState == null)
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.SHOP_PURCHASE_LIMIT_STATE_UNAVAILABLE,
                    $"Failed to create shop purchase limit state: productId={product.ProductId}");
            }

            refreshPurchaseLimit(limitState, product.ResetDays, serverNowUtcMs);
            if (limitState.purchaseCount >= product.MaxCount)
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.SHOP_PURCHASE_LIMIT_EXCEEDED,
                    $"Shop purchase limit exceeded: productId={product.ProductId}, maxCount={product.MaxCount}, purchaseCount={limitState.purchaseCount}, resetDays={product.ResetDays}");
            }

            return CommonResult.Ok();
        }

        bool hasRemainingPurchaseLimit(ShopProduct product, long serverNowUtcMs)
        {
            var state = default(ShopPurchaseLimitState);
            if (!_storage.TryGetPurchaseLimit(product.ProductId, out var persistedState) || persistedState == null)
            {
                state = new ShopPurchaseLimitState();
            }
            else
            {
                state = new ShopPurchaseLimitState
                {
                    periodStartUtcMs = persistedState.periodStartUtcMs,
                    purchaseCount = persistedState.purchaseCount,
                };
            }

            refreshPurchaseLimit(state, product.ResetDays, serverNowUtcMs);
            return state.purchaseCount < product.MaxCount;
        }

        static CommonResult validateProductConfig(string productId, out ShopProduct product)
        {
            product = null;
            if (string.IsNullOrWhiteSpace(productId))
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.SHOP_PRODUCT_ID_EMPTY,
                    "Shop productId is empty.");
            }

            if (!ShopProduct.TryGet(productId, out product))
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.SHOP_PRODUCT_NOT_FOUND,
                    $"Shop product not found: productId={productId}");
            }

            if (product.Price < 0)
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.SHOP_PRODUCT_PRICE_INVALID,
                    $"Shop product price is invalid: productId={product.ProductId}, price={product.Price}");
            }

            if (string.IsNullOrWhiteSpace(product.RewardGroupId))
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.SHOP_REWARD_GROUP_EMPTY,
                    $"Shop product rewardGroupId is empty: productId={product.ProductId}");
            }

            return CommonResult.Ok();
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

        static void refreshPurchaseLimit(ShopPurchaseLimitState limitState, int resetDays, long serverNowUtcMs)
        {
            var serverDayStartUtcMs = toUtcDayStart(serverNowUtcMs);
            if (serverDayStartUtcMs <= 0L)
            {
                limitState.periodStartUtcMs = 0L;
                limitState.purchaseCount = 0;
                return;
            }

            if (limitState.periodStartUtcMs <= 0L)
            {
                limitState.periodStartUtcMs = serverDayStartUtcMs;
                limitState.purchaseCount = 0;
                return;
            }

            var periodMs = (long)resetDays * MillisecondsPerDay;
            if (periodMs <= 0L)
            {
                limitState.periodStartUtcMs = serverDayStartUtcMs;
                limitState.purchaseCount = 0;
                return;
            }

            if (serverNowUtcMs < limitState.periodStartUtcMs
                || serverNowUtcMs >= limitState.periodStartUtcMs + periodMs)
            {
                limitState.periodStartUtcMs = serverDayStartUtcMs;
                limitState.purchaseCount = 0;
            }
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
