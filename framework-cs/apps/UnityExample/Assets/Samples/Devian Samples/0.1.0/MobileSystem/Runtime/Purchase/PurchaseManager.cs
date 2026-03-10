using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Devian.Domain.Common;
using Devian.Domain.Game;
#if UNITY_PURCHASING
using UnityEngine.Purchasing;
#endif

namespace Devian
{
    public sealed class PurchaseManager : CompoSingleton<PurchaseManager>
    {
        const string Tag = "PurchaseManager";
        const int MaxVerifyRecoveryRetries = 3;
        const int DefaultSeasonPurchaseBlockedBeforeEndDays = 3;

        [SerializeField] int _seasonPurchaseBlockedBeforeEndDays = DefaultSeasonPurchaseBlockedBeforeEndDays;

        readonly PurchaseStorage _storage = new();

        public PurchaseStorage Storage => _storage;
        public int SeasonPurchaseBlockedBeforeEndDays => _seasonPurchaseBlockedBeforeEndDays;

        protected override void onInitAwake()
        {
            _seasonPurchaseBlockedBeforeEndDays = Math.Max(0, _seasonPurchaseBlockedBeforeEndDays);
            SetProductCatalog(new GameProductCatalog());
            SetPurchaseStore(CreateDefaultStore());
        }

        public void SetSeasonPurchaseBlockedBeforeEndDays(int days)
        {
            _seasonPurchaseBlockedBeforeEndDays = Math.Max(0, days);
        }

        static IPurchaseStore CreateDefaultStore()
        {
#if UNITY_IOS || UNITY_TVOS
            return new DefaultPurchaseStore("apple");
#elif UNITY_ANDROID
            return new DefaultPurchaseStore("google");
#else
            return new DefaultPurchaseStore("unknown");
#endif
        }

        sealed class DefaultPurchaseStore : IPurchaseStore
        {
            public string StoreKey { get; }
            public DefaultPurchaseStore(string storeKey) { StoreKey = storeKey; }
            public string BuildVerifyPayload(string receipt) => receipt;
        }

        static RetryInterruptedPurchaseResult CreateNoOpRetryInterruptedPurchaseResult()
        {
            return new RetryInterruptedPurchaseResult(
                RetryInterruptedPurchaseStatus.SkippedNoCurrent,
                string.Empty,
                string.Empty,
                null,
                string.Empty,
                string.Empty,
                Array.Empty<RewardData>(),
                string.Empty,
                false);
        }

        static RefundResult CreateNoOpRefundResult()
        {
            return new RefundResult(
                0,
                0,
                0,
                0,
                0,
                0,
                string.Empty);
        }

        static PurchaseSyncResult CreateNoOpPurchaseSyncResult()
        {
            return new PurchaseSyncResult(
                CreateNoOpRetryInterruptedPurchaseResult(),
                null,
                CreateNoOpRefundResult(),
                null,
                null,
                null,
                null);
        }

        static string ResolveRewardGroupId(string internalProductId)
        {
            var product = TB_PURCHASE.Get(internalProductId);
            return product != null ? product.RewardGroupId ?? string.Empty : string.Empty;
        }

        static RewardData[] ResolveRewardDatas(string rewardGroupId)
        {
            if (string.IsNullOrEmpty(rewardGroupId))
                return Array.Empty<RewardData>();

            var rows = TB_REWARD.GetByGroup(rewardGroupId);
            if (rows == null || rows.Count == 0)
                return Array.Empty<RewardData>();

            var list = new List<RewardData>(rows.Count);
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null || string.IsNullOrEmpty(row.Id) || row.Amount <= 0)
                    continue;

                list.Add(new RewardData(row.Type, row.Id, row.Amount));
            }

            return list.Count == 0 ? Array.Empty<RewardData>() : list.ToArray();
        }

        static string ResolveSeasonPassId(string internalProductIdOrSeasonPassId)
        {
            if (string.IsNullOrEmpty(internalProductIdOrSeasonPassId))
                return string.Empty;

            var product = TB_PURCHASE.Get(internalProductIdOrSeasonPassId);
            if (product == null || product.Kind != PURCHASE_KIND.PASS)
                return internalProductIdOrSeasonPassId;

            var rewards = ResolveRewardDatas(product.RewardGroupId);
            for (var i = 0; i < rewards.Length; i++)
            {
                if (rewards[i].Type == REWARD_TYPE.PASS)
                    return rewards[i].Id;
            }

            return internalProductIdOrSeasonPassId;
        }

        CommonResult validateSeasonPurchaseWindow(PURCHASE product)
        {
            if (product == null || string.IsNullOrWhiteSpace(product.SeasonId))
                return CommonResult.Ok();

            var season = TB_SEASON.Get(product.SeasonId);
            if (season == null)
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    $"Season not found for product: productId={product.InternalProductId}, seasonId={product.SeasonId}");
            }

            var seasonEndUtcMs = season.EndUtcTime?.utcTimeMs ?? 0L;
            if (seasonEndUtcMs <= 0L)
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    $"Season end time is invalid: productId={product.InternalProductId}, seasonId={product.SeasonId}");
            }

            if (!RemoteConfigManager.TryGet(out var remoteConfigManager)
                || remoteConfigManager == null
                || !remoteConfigManager.TryGetServerNowUtcMs(out var serverNowUtcMs))
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.COMMON_SERVER,
                    "Server time is unavailable. Initialize RemoteConfigManager before purchase.");
            }

            var seasonPurchaseBlockedBeforeEnd = TimeSpan.FromDays(_seasonPurchaseBlockedBeforeEndDays);
            var blockStartUtcMs = seasonEndUtcMs - (long)seasonPurchaseBlockedBeforeEnd.TotalMilliseconds;
            if (serverNowUtcMs >= blockStartUtcMs)
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.PURCHASE_SEASON_END_SOON_BLOCKED,
                    $"Product purchase is blocked near season end: blockDays={_seasonPurchaseBlockedBeforeEndDays}, productId={product.InternalProductId}, seasonId={product.SeasonId}");
            }

            return CommonResult.Ok();
        }

        string ResolveStoreProductId(string internalProductId)
        {
            var product = TB_PURCHASE.Get(internalProductId);
            if (product == null)
                return internalProductId;

#if UNITY_IOS || UNITY_TVOS
            return string.IsNullOrEmpty(product.StoreSkuApple) ? internalProductId : product.StoreSkuApple;
#elif UNITY_ANDROID
            return string.IsNullOrEmpty(product.StoreSkuGoogle) ? internalProductId : product.StoreSkuGoogle;
#else
            return internalProductId;
#endif
        }

#if UNITY_EDITOR
        public Task<CommonResult<RefundResult>> RefundAsync(CancellationToken ct = default)
            => Task.FromResult(CommonResult<RefundResult>.Success(CreateNoOpRefundResult()));
#elif !UNITY_PURCHASING
        public Task<CommonResult<RefundResult>> RefundAsync(CancellationToken ct = default)
            => Task.FromResult(CommonResult<RefundResult>.Failure(
                COMMON_ERROR_TYPE.IAP_NOT_SUPPORTED,
                "Unity Purchasing not available."));
#else
        public async Task<CommonResult<RefundResult>> RefundAsync(CancellationToken ct = default)
        {
            int pageCount = 0;
            int handledCount = 0;
            int inventoryAppliedCount = 0;
            int noOpCount = 0;
            int skippedCount = 0;
            int ackFailedCount = 0;

            // Always start from the newest (cursor=empty) on each RefundAsync call.
            // desc ordering returns newest-first.
            // Server-side clientRefundApplied filter prevents duplicate processing.
            string pageCursor = string.Empty;

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                var prevCursor = pageCursor;
                var pageResult = await syncRefundsPageAsync(prevCursor, 50, ct);
                if (pageResult.IsFailure)
                    return CommonResult<RefundResult>.Failure(pageResult.Error!);

                var page = pageResult.Value!;
                pageCount++;

                for (var i = 0; i < page.Items.Length; i++)
                {
                    var item = page.Items[i];

                    // 보상 없는 환불 → 서버에 ACK만 보내고 넘어간다
                    if (item.Rewards == null || item.Rewards.Length == 0)
                    {
                        handledCount++;
                        noOpCount++;
                        if (!string.IsNullOrEmpty(item.PurchaseId))
                        {
                            var noOpAck = await ackRefundAppliedAsync(item.PurchaseId, ct);
                            if (noOpAck.IsFailure)
                            {
                                ackFailedCount++;
                                Debug.LogWarning($"[{Tag}] Refund ACK failed for no-op item. purchaseId={item.PurchaseId}: {noOpAck.Error?.Message}");
                            }
                        }
                        continue;
                    }

                    // RevokeRewards 실행 — 실패해도 다음 아이템으로 계속 진행
                    CommonResult apply;
                    try
                    {
                        apply = InventoryManager.Instance.RevokeRewardsPartial(item.Rewards);
                    }
                    catch (Exception ex)
                    {
                        skippedCount++;
                        Debug.LogWarning(
                            $"[{Tag}] Refund RevokeRewards exception (skipped). " +
                            $"purchaseId={item.PurchaseId} product={item.InternalProductId}: {ex.Message}");
                        continue;
                    }

                    if (apply.IsFailure)
                    {
                        skippedCount++;
                        Debug.LogWarning(
                            $"[{Tag}] Refund RevokeRewards failed (skipped). " +
                            $"purchaseId={item.PurchaseId} product={item.InternalProductId}: {apply.Error?.Message}");
                        continue;
                    }

                    handledCount++;
                    inventoryAppliedCount++;

                    // 서버에 ACK — 실패해도 다음 RefundAsync 호출에서 재처리됨 (중복 RevokeRewards 가능하나 서버 ACK 미완료이므로 재시도 정당)
                    if (!string.IsNullOrEmpty(item.PurchaseId))
                    {
                        var ack = await ackRefundAppliedAsync(item.PurchaseId, ct);
                        if (ack.IsFailure)
                        {
                            ackFailedCount++;
                            Debug.LogWarning($"[{Tag}] Refund ACK failed after revoke. purchaseId={item.PurchaseId}: {ack.Error?.Message}");
                        }
                    }
                }

                if (page.HasMore && string.IsNullOrEmpty(page.NextCursor))
                {
                    return CommonResult<RefundResult>.Failure(
                        COMMON_ERROR_TYPE.PURCHASE_REFUND_APPLY_FAILED,
                        "Refund sync cursor is empty while hasMore=true.");
                }

                if (page.HasMore && string.Equals(prevCursor, page.NextCursor ?? string.Empty, StringComparison.Ordinal))
                {
                    return CommonResult<RefundResult>.Failure(
                        COMMON_ERROR_TYPE.PURCHASE_REFUND_APPLY_FAILED,
                        "Refund sync cursor did not advance.");
                }

                pageCursor = page.NextCursor ?? string.Empty;
                getPurchaseStorageOrNull()?.PruneRefundSupportLogs();

                if (!page.HasMore)
                {
                    return CommonResult<RefundResult>.Success(
                        new RefundResult(
                            pageCount,
                            handledCount,
                            inventoryAppliedCount,
                            noOpCount,
                            skippedCount,
                            ackFailedCount,
                            pageCursor));
                }
            }
        }
#endif

        /// <summary>
        /// initSession에서 pre-loaded된 첫 페이지로 시작하는 RefundAsync.
        /// 첫 페이지는 서버 호출 없이 처리하고, hasMore일 때만 후속 페이지를 서버에서 가져온다.
        /// </summary>
        async Task<CommonResult<RefundResult>> refundWithPreloadedPageAsync(
            RefundPageResult preloadedFirstPage, CancellationToken ct)
        {
            int pageCount = 0;
            int handledCount = 0;
            int inventoryAppliedCount = 0;
            int noOpCount = 0;
            int skippedCount = 0;
            int ackFailedCount = 0;

            string pageCursor = string.Empty;
            bool usePreloaded = true;

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                RefundSyncResult page;
                string prevCursor = pageCursor;

                if (usePreloaded)
                {
                    page = enrichAdjustmentItems(preloadedFirstPage);
                    usePreloaded = false;
                }
                else
                {
                    var pageResult = await syncRefundsPageAsync(prevCursor, 50, ct);
                    if (pageResult.IsFailure)
                        return CommonResult<RefundResult>.Failure(pageResult.Error!);
                    page = pageResult.Value!;
                }

                pageCount++;

                for (var i = 0; i < page.Items.Length; i++)
                {
                    var item = page.Items[i];

                    if (item.Rewards == null || item.Rewards.Length == 0)
                    {
                        handledCount++;
                        noOpCount++;
                        if (!string.IsNullOrEmpty(item.PurchaseId))
                        {
                            var noOpAck = await ackRefundAppliedAsync(item.PurchaseId, ct);
                            if (noOpAck.IsFailure)
                            {
                                ackFailedCount++;
                                Debug.LogWarning($"[{Tag}] Refund ACK failed for no-op item. purchaseId={item.PurchaseId}: {noOpAck.Error?.Message}");
                            }
                        }
                        continue;
                    }

                    CommonResult apply;
                    try
                    {
                        apply = InventoryManager.Instance.RevokeRewardsPartial(item.Rewards);
                    }
                    catch (Exception ex)
                    {
                        skippedCount++;
                        Debug.LogWarning(
                            $"[{Tag}] Refund RevokeRewards exception (skipped). " +
                            $"purchaseId={item.PurchaseId} product={item.InternalProductId}: {ex.Message}");
                        continue;
                    }

                    if (apply.IsFailure)
                    {
                        skippedCount++;
                        Debug.LogWarning(
                            $"[{Tag}] Refund RevokeRewards failed (skipped). " +
                            $"purchaseId={item.PurchaseId} product={item.InternalProductId}: {apply.Error?.Message}");
                        continue;
                    }

                    handledCount++;
                    inventoryAppliedCount++;

                    if (!string.IsNullOrEmpty(item.PurchaseId))
                    {
                        var ack = await ackRefundAppliedAsync(item.PurchaseId, ct);
                        if (ack.IsFailure)
                        {
                            ackFailedCount++;
                            Debug.LogWarning($"[{Tag}] Refund ACK failed after revoke. purchaseId={item.PurchaseId}: {ack.Error?.Message}");
                        }
                    }
                }

                if (page.HasMore && string.IsNullOrEmpty(page.NextCursor))
                {
                    return CommonResult<RefundResult>.Failure(
                        COMMON_ERROR_TYPE.PURCHASE_REFUND_APPLY_FAILED,
                        "Refund sync cursor is empty while hasMore=true.");
                }

                if (page.HasMore && string.Equals(prevCursor, page.NextCursor ?? string.Empty, StringComparison.Ordinal))
                {
                    return CommonResult<RefundResult>.Failure(
                        COMMON_ERROR_TYPE.PURCHASE_REFUND_APPLY_FAILED,
                        "Refund sync cursor did not advance.");
                }

                pageCursor = page.NextCursor ?? string.Empty;
                getPurchaseStorageOrNull()?.PruneRefundSupportLogs();

                if (!page.HasMore)
                {
                    return CommonResult<RefundResult>.Success(
                        new RefundResult(
                            pageCount,
                            handledCount,
                            inventoryAppliedCount,
                            noOpCount,
                            skippedCount,
                            ackFailedCount,
                            pageCursor));
                }
            }
        }

#if UNITY_PURCHASING
        StoreController _controller;
        bool _iapInitialized;
        string _initError;

        Task<CommonResult> _initializeTask;

        TaskCompletionSource<PendingOrder> _purchaseTcs;
        bool _purchaseInProgress;
        readonly List<PendingOrder> _deferredPendingOrders = new List<PendingOrder>();

        IPurchaseStore _purchaseStore;
        IPurchaseProductCatalog _productCatalog;

        public void SetPurchaseStore(IPurchaseStore store)
        {
            _purchaseStore = store;
        }

        public void SetProductCatalog(IPurchaseProductCatalog catalog)
        {
            _productCatalog = catalog;
        }

        // ── Public API ─────────────────────────────────────────────

        /// <summary>
        /// IAP 초기화를 명시적으로 수행한다.
        /// 여러 번 호출해도 동일 Task를 반환한다 (idempotent).
        /// Editor에서는 즉시 PURCHASE_UNSUPPORTED_PLATFORM 반환.
        /// </summary>
        public Task<CommonResult> InitializeAsync(CancellationToken ct = default)
        {
#if UNITY_EDITOR
            return Task.FromResult(CommonResult.Failure(
                COMMON_ERROR_TYPE.PURCHASE_UNSUPPORTED_PLATFORM,
                "PurchaseManager is not supported in Editor."));
#else
            if (_initializeTask != null)
            {
                // 이전 초기화가 실패했으면 캐시를 리셋하여 재시도를 허용한다.
                if (_initializeTask.IsCompleted && _initializeTask.Result.IsFailure)
                {
                    _initializeTask = null;
                }
                else
                {
                    return _initializeTask;
                }
            }

            _initializeTask = initializeIapAsync(ct);
            return _initializeTask;
#endif
        }

        /// <summary>
        /// Session restore/login 직후 구매 상태를 정합화한다.
        /// 순서:
        /// 1. IAP initialize
        /// 2. interrupted purchase retry
        /// 3. refund apply
        /// 4. entitlements sync
        /// 5. local/cloud save (non-fatal)
        /// Retry/refund/entitlements/save 실패는 경고로 남기고 전체 sync는 계속 진행한다.
        /// Initialize 실패만 fatal로 처리한다.
        /// </summary>
#if UNITY_EDITOR
        public Task<CommonResult<PurchaseSyncResult>> SyncAsync(
            SessionInitSnapshot? initSnapshot = null, CancellationToken ct = default)
            => Task.FromResult(CommonResult<PurchaseSyncResult>.Success(CreateNoOpPurchaseSyncResult()));
#else
        public async Task<CommonResult<PurchaseSyncResult>> SyncAsync(
            SessionInitSnapshot? initSnapshot = null, CancellationToken ct = default)
        {
            var init = await InitializeAsync(ct);
            if (init.IsFailure)
                return CommonResult<PurchaseSyncResult>.Failure(init.Error!);

            RetryInterruptedPurchaseResult? retryInterruptedPurchase = null;
            CommonError retryInterruptedError = null;

            var retry = await RetryInterruptedPurchaseAsync(ct);
            if (retry.IsSuccess)
            {
                retryInterruptedPurchase = retry.Value;
            }
            else
            {
                retryInterruptedError = retry.Error;
                Debug.LogWarning($"[{Tag}] RetryInterruptedPurchaseAsync failed during sync (non-fatal): {retryInterruptedError}");
            }

            RefundResult? refund = null;
            CommonError refundError = null;

            var refundResult = initSnapshot.HasValue
                ? await refundWithPreloadedPageAsync(initSnapshot.Value.PurchaseAdjustments, ct)
                : await RefundAsync(ct);
            if (refundResult.IsSuccess)
            {
                refund = refundResult.Value;
            }
            else
            {
                refundError = refundResult.Error;
                Debug.LogWarning($"[{Tag}] RefundAsync failed during sync (non-fatal): {refundError}");
            }

            EntitlementsSnapshot? entitlements = null;
            CommonError entitlementsError = null;

            if (initSnapshot.HasValue)
            {
                var snapshot = resolveEntitlementsSeasonPasses(initSnapshot.Value.Entitlements);
                cacheEntitlementsSnapshot(snapshot);
                entitlements = snapshot;
            }
            else
            {
                var entitlementsResult = await SyncEntitlementsAsync(ct);
                if (entitlementsResult.IsSuccess)
                {
                    entitlements = entitlementsResult.Value;
                }
                else
                {
                    entitlementsError = entitlementsResult.Error;
                    Debug.LogWarning($"[{Tag}] SyncEntitlementsAsync failed during sync (non-fatal): {entitlementsError}");
                }
            }

            CommonError saveError = null;
            var save = await SaveDataManager.Instance.SaveGameStorageAsync(true, ct);
            if (save.IsFailure)
            {
                saveError = save.Error;
                Debug.LogWarning($"[{Tag}] Post-sync save failed (non-fatal): {saveError}");
            }

            return CommonResult<PurchaseSyncResult>.Success(
                new PurchaseSyncResult(
                    retryInterruptedPurchase,
                    retryInterruptedError,
                    refund,
                    refundError,
                    entitlements,
                    entitlementsError,
                    saveError));
        }
#endif

        /// <summary>
        /// 상품 구매를 수행한다. TB_PURCHASE에서 Kind를 조회하여 자동으로 구매 유형을 결정한다.
        /// </summary>
        public async Task<CommonResult<PurchaseFinalResult>> PurchaseAsync(
            string internalProductId, CancellationToken ct = default)
        {
            var product = TB_PURCHASE.Get(internalProductId);
            if (product == null)
                return CommonResult<PurchaseFinalResult>.Failure(
                    COMMON_ERROR_TYPE.PURCHASE_NOT_FOUND,
                    $"Product not found: {internalProductId}");

            var seasonWindow = validateSeasonPurchaseWindow(product);
            if (seasonWindow.IsFailure)
                return CommonResult<PurchaseFinalResult>.Failure(seasonWindow.Error!);

            var kind = ProductKindToPurchaseKind(product.Kind);
            var result = await purchaseAndVerifyAsync(internalProductId, kind, ct);
            if (result.IsSuccess)
            {
                var final_ = result.Value!;
                await applyClientGrantIfNeededAsync(
                    final_.PurchaseId, final_.AppliedRewards,
                    final_.NeedsClientGrantDelivery, ct);

                // 구매 결과를 local + cloud에 저장 (cloud 실패는 non-fatal)
                var save = await SaveDataManager.Instance.SaveGameStorageAsync(true, ct);
                if (save.IsFailure)
                    Debug.LogWarning($"[{Tag}] Post-purchase save failed (non-fatal): {save.Error}");
            }
            return result;
        }

#if UNITY_EDITOR
        public Task<CommonResult<RetryInterruptedPurchaseResult>> RetryInterruptedPurchaseAsync(CancellationToken ct = default)
            => Task.FromResult(CommonResult<RetryInterruptedPurchaseResult>.Success(CreateNoOpRetryInterruptedPurchaseResult()));
#else
        public async Task<CommonResult<RetryInterruptedPurchaseResult>> RetryInterruptedPurchaseAsync(CancellationToken ct = default)
        {
            var purchaseStorage = getPurchaseStorageOrNull();
            if (purchaseStorage == null)
                return CommonResult<RetryInterruptedPurchaseResult>.Failure(
                    COMMON_ERROR_TYPE.PURCHASE_INTERRUPTED_STORAGE_UNAVAILABLE,
                    "PurchaseStorage is not available.");

            var current = purchaseStorage.Current;
            if (!current.IsPurchaseInProgress)
                return CommonResult<RetryInterruptedPurchaseResult>.Success(
                    new RetryInterruptedPurchaseResult(
                        RetryInterruptedPurchaseStatus.SkippedNoCurrent,
                        string.Empty,
                        string.Empty,
                        null,
                        string.Empty,
                        string.Empty,
                        Array.Empty<RewardData>(),
                        string.Empty,
                        false));

            if (string.IsNullOrEmpty(current.InternalProductId))
                return CommonResult<RetryInterruptedPurchaseResult>.Failure(
                    COMMON_ERROR_TYPE.PURCHASE_INTERRUPTED_SNAPSHOT_PRODUCT_ID_MISSING,
                    "Interrupted purchase snapshot is missing internalProductId.");

            if (!TryResolveInterruptedPurchaseKind(current.InternalProductId, current.Kind, out var currentKind))
            {
                return CommonResult<RetryInterruptedPurchaseResult>.Failure(
                    COMMON_ERROR_TYPE.PURCHASE_INTERRUPTED_SNAPSHOT_KIND_INVALID,
                    $"Interrupted purchase snapshot has invalid purchase kind: {current.Kind}");
            }

            if (current.StoreConfirmedLocal && !string.IsNullOrEmpty(current.PurchaseId))
            {
                // resumeAfterStoreConfirmAsync는 purchaseAndVerifyAsync를 거치지 않으므로
                // _purchaseInProgress 가드를 여기서 직접 관리한다.
                if (_purchaseInProgress)
                    return CommonResult<RetryInterruptedPurchaseResult>.Failure(
                        COMMON_ERROR_TYPE.PURCHASE_PURCHASE_IN_PROGRESS,
                        "Another purchase is already in progress.");

                _purchaseInProgress = true;
                try
                {
                    var resumed = await resumeAfterStoreConfirmAsync(current.InternalProductId, currentKind, ct);
                    if (resumed.IsFailure)
                        return CommonResult<RetryInterruptedPurchaseResult>.Failure(resumed.Error!);

                    var finalAfterConfirm = resumed.Value!;
                    await applyClientGrantIfNeededAsync(
                        finalAfterConfirm.PurchaseId, finalAfterConfirm.AppliedRewards,
                        finalAfterConfirm.NeedsClientGrantDelivery, ct);
                    return CommonResult<RetryInterruptedPurchaseResult>.Success(
                        new RetryInterruptedPurchaseResult(
                            RetryInterruptedPurchaseStatus.Retried,
                            finalAfterConfirm.PurchaseId,
                            finalAfterConfirm.InternalProductId,
                            finalAfterConfirm.Kind,
                            finalAfterConfirm.ResultStatus,
                            finalAfterConfirm.RewardGroupId,
                            finalAfterConfirm.AppliedRewards,
                            finalAfterConfirm.ClientGrantStatus,
                            finalAfterConfirm.NeedsClientGrantDelivery));
                }
                finally
                {
                    _purchaseInProgress = false;
                }
            }

            var resume = await purchaseAndVerifyAsync(current.InternalProductId, currentKind, ct, isRecoveryCall: true);
            if (resume.IsFailure)
                return CommonResult<RetryInterruptedPurchaseResult>.Failure(resume.Error!);

            var finalResult = resume.Value!;
            await applyClientGrantIfNeededAsync(
                finalResult.PurchaseId, finalResult.AppliedRewards,
                finalResult.NeedsClientGrantDelivery, ct);
            return CommonResult<RetryInterruptedPurchaseResult>.Success(
                new RetryInterruptedPurchaseResult(
                    RetryInterruptedPurchaseStatus.Retried,
                    finalResult.PurchaseId,
                    finalResult.InternalProductId,
                    finalResult.Kind,
                    finalResult.ResultStatus,
                    finalResult.RewardGroupId,
                    finalResult.AppliedRewards,
                    finalResult.ClientGrantStatus,
                    finalResult.NeedsClientGrantDelivery));
        }
#endif

        public Task<CommonResult> AckPurchaseClientGrantAppliedAsync(string purchaseId, CancellationToken ct = default)
            => completePurchaseClientGrantAsync(purchaseId, "APPLIED_ACKED", ct);

        public Task<CommonResult> ReportPurchaseClientGrantFailureAsync(string purchaseId, CancellationToken ct = default)
            => completePurchaseClientGrantAsync(purchaseId, "FAILED_REPORTED", ct);

        private async Task<CommonResult> applyClientGrantIfNeededAsync(
            string purchaseId, RewardData[] rewards, bool needsClientGrantDelivery, CancellationToken ct)
        {
            if (!needsClientGrantDelivery)
                return CommonResult.Ok();

            var apply = RewardManager.Instance.ApplyRewardDatas(rewards);
            if (apply.IsSuccess)
            {
                var ack = await AckPurchaseClientGrantAppliedAsync(purchaseId, ct);
                if (ack.IsFailure)
                    UnityEngine.Debug.LogWarning($"[PurchaseManager] AckClientGrant failed (non-fatal): {ack.Error}");
                return CommonResult.Ok();
            }

            UnityEngine.Debug.LogWarning($"[PurchaseManager] ApplyRewardDatas failed: {apply.Error}");
            var report = await ReportPurchaseClientGrantFailureAsync(purchaseId, ct);
            if (report.IsFailure)
                UnityEngine.Debug.LogWarning($"[PurchaseManager] ReportClientGrantFailure failed (non-fatal): {report.Error}");
            return apply;
        }

        // Store restore only (manual/fallback). Domain grant/revoke handling is managed by caller-side sync.
        public async Task<CommonResult<EntitlementsSnapshot>> RestoreAsync(CancellationToken ct = default)
        {
            if (!_iapInitialized)
            {
                var init = await InitializeAsync(ct);
                if (init.IsFailure)
                    return CommonResult<EntitlementsSnapshot>.Failure(init.Error!);
            }

            var tcs = new TaskCompletionSource<CommonResult<bool>>();

            _controller.RestoreTransactions((success, error) =>
            {
                if (success)
                    tcs.TrySetResult(CommonResult<bool>.Success(true));
                else
                    tcs.TrySetResult(CommonResult<bool>.Failure(COMMON_ERROR_TYPE.PURCHASE_RESTORE_FAILED, error ?? "RestoreTransactions failed."));
            });

            if (ct.CanBeCanceled)
                ct.Register(() => tcs.TrySetCanceled(ct));

            var restore = await tcs.Task;
            if (restore.IsFailure)
                return CommonResult<EntitlementsSnapshot>.Failure(restore.Error!);

            return await SyncEntitlementsAsync(ct);
        }

        // SyncEntitlementsAsync updates InventoryStorage for Rental expiry and SeasonPass ownership.
        // Rental remaining time is queried from server on demand via GetRentalRemainingMsAsync().
        // noAds 판단은 게임 로직이 남은 시간(ms) 기반으로 처리한다.
        public async Task<CommonResult<EntitlementsSnapshot>> SyncEntitlementsAsync(CancellationToken ct = default)
        {
            var result = await FirebaseCallableManager.Instance.GetEntitlementsAsync(ct);
            if (result.IsFailure)
                return CommonResult<EntitlementsSnapshot>.Failure(result.Error!);

            var snapshot = resolveEntitlementsSeasonPasses(result.Value!);
            cacheEntitlementsSnapshot(snapshot);
            return CommonResult<EntitlementsSnapshot>.Success(snapshot);
        }

        public async Task<CommonResult<long>> GetRentalRemainingMsAsync(string internalProductId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(internalProductId))
                return CommonResult<long>.Failure(COMMON_ERROR_TYPE.PURCHASE_INTERNAL_PRODUCT_ID_EMPTY, "internalProductId is required.");

            var sync = await SyncEntitlementsAsync(ct);
            if (sync.IsFailure)
                return CommonResult<long>.Failure(sync.Error!);

            var snapshot = sync.Value!;
            if (!snapshot.Rentals.TryGetValue(internalProductId, out var expiresAtUtcMs) || expiresAtUtcMs <= 0)
                return CommonResult<long>.Success(0L);

            var serverNowUtcMs = snapshot.ServerNowUtcMs > 0
                ? snapshot.ServerNowUtcMs
                : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var remainingMs = expiresAtUtcMs - serverNowUtcMs;
            var clampedRemainingMs = remainingMs > 0 ? remainingMs : 0L;
            return CommonResult<long>.Success(clampedRemainingMs);
        }

#if UNITY_EDITOR
        public Task<CommonResult<RecentPurchaseItem>> GetLatestConsumablePurchase30dAsync(CancellationToken ct = default)
        {
            return Task.FromResult(CommonResult<RecentPurchaseItem>.Failure(
                COMMON_ERROR_TYPE.PURCHASE_UNSUPPORTED_PLATFORM,
                "PurchaseManager is not supported in Editor."));
        }
#else
        public async Task<CommonResult<RecentPurchaseItem>> GetLatestConsumablePurchase30dAsync(CancellationToken ct = default)
        {
            if (!_iapInitialized)
                return CommonResult<RecentPurchaseItem>.Failure(
                    COMMON_ERROR_TYPE.PURCHASE_INIT_REQUIRED,
                    "PurchaseManager not initialized. Call InitializeAsync() first.");

            var data = new Dictionary<string, object> { ["pageSize"] = 1, ["kind"] = "Consumable" };
            return await FirebaseCallableManager.Instance.GetRecentPurchases30dAsync(data, ct);
        }
#endif

        async Task<CommonResult<RefundSyncResult>> syncRefundsPageAsync(
            string cursor = null,
            int pageSize = 50,
            CancellationToken ct = default)
        {
            if (pageSize <= 0)
                pageSize = 50;
            if (pageSize > 200)
                pageSize = 200;

            var data = new Dictionary<string, object>
            {
                ["pageSize"] = pageSize,
            };
            if (!string.IsNullOrEmpty(cursor))
                data["cursor"] = cursor;

            var result = await FirebaseCallableManager.Instance.GetPurchaseAdjustmentsAsync(data, ct);
            if (result.IsFailure)
                return CommonResult<RefundSyncResult>.Failure(result.Error!);

            // Server filters out already-ACKed items (clientRefundApplied===true).
            // Client-side dedup is no longer needed.
            var syncResult = enrichAdjustmentItems(result.Value!);
            return CommonResult<RefundSyncResult>.Success(syncResult);
        }

        // ── Core Flow ──────────────────────────────────────────────

        async Task<CommonResult> initializeIapAsync(CancellationToken ct)
        {
            if (_productCatalog == null)
                return CommonResult.Failure(COMMON_ERROR_TYPE.PURCHASE_INIT_FAILED, "ProductCatalog not set. Call SetProductCatalog().");

            try
            {
                _controller = UnityIAPServices.StoreController();

                _controller.OnPurchasePending += onPurchasePending;
                _controller.OnPurchaseFailed += onPurchaseFailed;
                _controller.OnStoreDisconnected += onStoreDisconnected;

                await _controller.Connect();
                Debug.Log($"[{Tag}] Store connected.");

                ct.ThrowIfCancellationRequested();

                var items = _productCatalog.GetActiveProducts();
                var definitions = new List<ProductDefinition>(items.Count);
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    definitions.Add(new ProductDefinition(
                        item.InternalProductId, item.StoreSku, toUnityProductType(item.ProductType)));
                }

                var fetchTcs = new TaskCompletionSource<bool>();

                void onFetched(List<Product> fetched) => fetchTcs.TrySetResult(true);
                void onFetchFailed(ProductFetchFailed failure)
                    => fetchTcs.TrySetException(new Exception($"Products fetch failed: {failure}"));

                _controller.OnProductsFetched += onFetched;
                _controller.OnProductsFetchFailed += onFetchFailed;

                try
                {
                    _controller.FetchProducts(definitions);
                    Debug.Log($"[{Tag}] IAP initializing... products={definitions.Count}");

                    if (ct.CanBeCanceled)
                        ct.Register(() => fetchTcs.TrySetCanceled(ct));

                    await fetchTcs.Task;
                }
                finally
                {
                    _controller.OnProductsFetched -= onFetched;
                    _controller.OnProductsFetchFailed -= onFetchFailed;
                }

                _iapInitialized = true;
                Debug.Log($"[{Tag}] IAP initialized successfully.");
                return CommonResult.Ok();
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _initError = ex.Message;
                Debug.LogError($"[{Tag}] IAP initialization failed: {ex.Message}");

                if (ex.Message != null && ex.Message.Contains("Products fetch failed"))
                    return CommonResult.Failure(COMMON_ERROR_TYPE.PURCHASE_FETCH_FAILED, ex.Message);

                return CommonResult.Failure(COMMON_ERROR_TYPE.PURCHASE_INIT_FAILED, ex.Message);
            }
        }

        async Task<CommonResult<PurchaseFinalResult>> purchaseAndVerifyAsync(
            string internalProductId, PurchaseKind kind, CancellationToken ct, bool isRecoveryCall = false)
        {
            if (!_iapInitialized)
            {
                var init = await InitializeAsync(ct);
                if (init.IsFailure)
                    return CommonResult<PurchaseFinalResult>.Failure(init.Error!);
            }

            var purchaseLoginReady = await ensurePurchaseLoginReadyAsync(ct);
            if (purchaseLoginReady.IsFailure)
                Debug.LogWarning($"[{Tag}] purchase login readiness failed: {purchaseLoginReady.Error}");
            if (purchaseLoginReady.IsFailure || !purchaseLoginReady.Value)
                return CommonResult<PurchaseFinalResult>.Failure(COMMON_ERROR_TYPE.PURCHASE_UNAUTHENTICATED,
                    "Authentication required before purchase. Sign in with Guest, Google, or Apple first.");

            if (_purchaseStore == null)
                return CommonResult<PurchaseFinalResult>.Failure(COMMON_ERROR_TYPE.PURCHASE_STORE_NOT_SET, "PurchaseStore not set. Call SetPurchaseStore().");

            if (_purchaseInProgress)
                return CommonResult<PurchaseFinalResult>.Failure(COMMON_ERROR_TYPE.PURCHASE_PURCHASE_IN_PROGRESS, "Another purchase is already in progress.");

            var purchaseStorage = getPurchaseStorageOrNull();
            if (!isRecoveryCall && (purchaseStorage?.Current.IsPurchaseInProgress ?? false))
            {
                return CommonResult<PurchaseFinalResult>.Failure(
                    COMMON_ERROR_TYPE.PURCHASE_PURCHASE_IN_PROGRESS,
                    "Interrupted purchase exists. Call RetryInterruptedPurchaseAsync() before starting a new purchase.");
            }

            var clearCurrentOnExit = true;
            var purchaseKindString = PurchaseKindToString(kind);
            var recoverClientGrantApplied =
                (purchaseStorage?.Current.IsPurchaseInProgress ?? false) &&
                string.Equals(purchaseStorage.Current.InternalProductId, internalProductId, StringComparison.Ordinal) &&
                string.Equals(purchaseStorage.Current.Kind, purchaseKindString, StringComparison.Ordinal) &&
                string.Equals(purchaseStorage.Current.StoreKey, _purchaseStore.StoreKey, StringComparison.Ordinal) &&
                purchaseStorage.Current.ClientGrantApplied;
            var recoverClientGrantReported =
                (purchaseStorage?.Current.IsPurchaseInProgress ?? false) &&
                string.Equals(purchaseStorage.Current.InternalProductId, internalProductId, StringComparison.Ordinal) &&
                string.Equals(purchaseStorage.Current.Kind, purchaseKindString, StringComparison.Ordinal) &&
                string.Equals(purchaseStorage.Current.StoreKey, _purchaseStore.StoreKey, StringComparison.Ordinal) &&
                purchaseStorage.Current.ClientGrantReported;
            var recoverStoreConfirmedLocal =
                (purchaseStorage?.Current.IsPurchaseInProgress ?? false) &&
                string.Equals(purchaseStorage.Current.InternalProductId, internalProductId, StringComparison.Ordinal) &&
                string.Equals(purchaseStorage.Current.Kind, purchaseKindString, StringComparison.Ordinal) &&
                string.Equals(purchaseStorage.Current.StoreKey, _purchaseStore.StoreKey, StringComparison.Ordinal) &&
                purchaseStorage.Current.StoreConfirmedLocal;
            _purchaseInProgress = true;
            try
            {
                // Recovery call은 이전 세션의 Current 스냅샷(PurchaseId, StoreConfirmedLocal 등)을
                // 보존해야 한다. BeginPurchase()는 이를 초기화하므로, recovery 시에는 스킵한다.
                if (!isRecoveryCall)
                    purchaseStorage?.BeginPurchase(internalProductId, purchaseKindString, _purchaseStore.StoreKey);

                PendingOrder pendingOrder;

                var storeProductId = ResolveStoreProductId(internalProductId);

                // Consumable/Rental: 스토어에 미소비 아이템이 이미 있을 수 있으므로
                // PurchaseProduct() 호출 전에 FetchPurchases()로 먼저 확인한다.
                // - Recovery call: 이전 세션에서 중단된 구매의 미소비 아이템 복구
                // - Normal call: 앱 재설치 등으로 PurchaseStorage가 초기화되었지만 스토어에 미소비 잔류하는 경우
                // 이를 통해 불필요한 "already owned" 에러를 사전에 회피한다.
                if (isRepurchasableStoreKind(kind) &&
                    !TryTakeDeferredPendingOrder(storeProductId, out pendingOrder))
                {
                    Debug.Log($"[{Tag}] Pre-fetching pending purchases before PurchaseProduct (isRecovery={isRecoveryCall}). storeProductId={storeProductId}");
                    var preFetch = await fetchPendingPurchasesAsync(ct);
                    if (preFetch.IsSuccess)
                    {
                        // 1차: deferred queue (OnPurchasePending이 새 transactionID에 대해 발생한 경우)
                        if (TryTakeDeferredPendingOrder(storeProductId, out pendingOrder))
                        {
                            purchaseStorage?.MarkStorePending();
                            Debug.Log($"[{Tag}] Pre-fetch: found pending order in deferred queue. storeProductId={storeProductId}");
                            goto PendingOrderReady;
                        }
                        // 2차: Orders.PendingOrders 직접 검색
                        if (TryFindPendingOrderFromFetchedOrders(preFetch.Value, storeProductId, out pendingOrder))
                        {
                            purchaseStorage?.MarkStorePending();
                            Debug.Log($"[{Tag}] Pre-fetch: found pending order directly from fetched Orders.PendingOrders. storeProductId={storeProductId}");
                            goto PendingOrderReady;
                        }

                        // 동일 storeProductId의 stale pending orders만 ConfirmPurchase로 소비 처리
                        // (서버 검증 실패로 stuck된 취소/만료 구매를 정리하여 재구매를 가능하게 함)
                        // 다른 상품의 pending order는 건드리지 않는다.
                        if (preFetch.Value?.PendingOrders != null && preFetch.Value.PendingOrders.Count > 0)
                        {
                            Debug.Log($"[{Tag}] Pre-fetch: checking {preFetch.Value.PendingOrders.Count} pending order(s) for stale cleanup. storeProductId={storeProductId}");
                            foreach (var stale in preFetch.Value.PendingOrders)
                            {
                                var staleStoreProductId = TryExtractStoreProductIdFromReceipt(stale.Info.Receipt);
                                if (string.IsNullOrEmpty(staleStoreProductId) ||
                                    !string.Equals(staleStoreProductId, storeProductId, StringComparison.Ordinal))
                                {
                                    Debug.Log($"[{Tag}] Skipping stale pending order (different product). staleProductId={staleStoreProductId}, expected={storeProductId}");
                                    continue;
                                }

                                try
                                {
                                    Debug.Log($"[{Tag}] Consuming stale pending order. transactionID={stale.Info.TransactionID}");
                                    _controller.ConfirmPurchase(stale);
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogWarning($"[{Tag}] ConfirmPurchase for stale order failed: {ex.Message}");
                                }
                            }
                        }

                        Debug.Log($"[{Tag}] Pre-fetch: no matching pending order found. Proceeding to PurchaseProduct. storeProductId={storeProductId}");
                    }
                    else
                    {
                        Debug.LogWarning($"[{Tag}] Pre-fetch failed: {preFetch.Error?.Message}. Proceeding to PurchaseProduct. storeProductId={storeProductId}");
                    }
                }

                if (!TryTakeDeferredPendingOrder(storeProductId, out pendingOrder))
                {
                    _purchaseTcs = new TaskCompletionSource<PendingOrder>();
                    _controller.PurchaseProduct(storeProductId);

                    var gotPendingOrder = false;
                    try
                    {
                        pendingOrder = await _purchaseTcs.Task;
                        gotPendingOrder = true;
                    }
                    catch (Exception ex)
                    {
                        if (isRepurchasableStoreKind(kind) && isStoreAlreadyOwnedFailureMessage(ex.Message))
                        {
                            // Some stores may raise "already owned" before replaying a pending/owned transaction callback.
                            // Poll the deferred queue briefly so Consumable/Rental purchases can recover in the same call.
                            const int alreadyOwnedRecoveryPollCount = 50; // ~5s
                            for (var i = 0; i < alreadyOwnedRecoveryPollCount; i++)
                            {
                                if (TryTakeDeferredPendingOrder(storeProductId, out pendingOrder))
                                {
                                    purchaseStorage?.MarkStorePending();
                                    gotPendingOrder = true;
                                    Debug.LogWarning($"[{Tag}] Store reported already-owned for repurchasable item, recovered deferred pending order. storeProductId={storeProductId}");
                                    break;
                                }

                                await Task.Delay(100);
                            }

                            if (!gotPendingOrder)
                            {
                                // Polling alone failed — force-fetch unconsumed purchases from the store.
                                // Unity IAP's WasPurchaseAlreadyProcessed blocks OnPurchasePending for
                                // transactions already seen in this session, so we must read Orders.PendingOrders
                                // directly instead of relying on the _deferredPendingOrders callback queue.
                                Debug.LogWarning(
                                    $"[{Tag}] Deferred polling failed for already-owned item. " +
                                    $"Attempting FetchPurchases fallback (direct Orders access). storeProductId={storeProductId}");

                                var fetchResult = await fetchPendingPurchasesAsync(ct);
                                if (fetchResult.IsSuccess)
                                {
                                    var fetchedOrders = fetchResult.Value;

                                    // 1차: _deferredPendingOrders 확인 (새 세션이거나 다른 transactionID인 경우 OnPurchasePending이 발생할 수 있음)
                                    if (TryTakeDeferredPendingOrder(storeProductId, out pendingOrder))
                                    {
                                        purchaseStorage?.MarkStorePending();
                                        gotPendingOrder = true;
                                        Debug.LogWarning($"[{Tag}] Recovered pending order via deferred queue after FetchPurchases. storeProductId={storeProductId}");
                                    }
                                    // 2차: Orders.PendingOrders에서 직접 검색 (WasPurchaseAlreadyProcessed 우회)
                                    else if (TryFindPendingOrderFromFetchedOrders(fetchedOrders, storeProductId, out pendingOrder))
                                    {
                                        purchaseStorage?.MarkStorePending();
                                        gotPendingOrder = true;
                                        Debug.LogWarning($"[{Tag}] Recovered pending order directly from FetchPurchases Orders.PendingOrders. storeProductId={storeProductId}");
                                    }
                                    else
                                    {
                                        Debug.LogWarning(
                                            $"[{Tag}] FetchPurchases succeeded but no matching pending order found " +
                                            $"(pendingOrders={fetchedOrders?.PendingOrders?.Count ?? 0}). storeProductId={storeProductId}");
                                    }
                                }
                                else
                                {
                                    Debug.LogWarning(
                                        $"[{Tag}] FetchPurchases fallback failed: {fetchResult.Error?.Message}. storeProductId={storeProductId}");
                                }
                            }
                        }

                        if (gotPendingOrder)
                            goto PendingOrderReady;

                        return CommonResult<PurchaseFinalResult>.Failure(COMMON_ERROR_TYPE.PURCHASE_PURCHASE_REQUEST_FAILED, ex.Message);
                    }
                    finally
                    {
                        _purchaseTcs = null;
                    }
                }
                else
                {
                    purchaseStorage?.MarkStorePending();
                    Debug.Log($"[{Tag}] Reusing deferred pending order for {storeProductId}.");
                }

            PendingOrderReady:
                var finalizeCt = ct;
                if (ct.IsCancellationRequested)
                {
                    // Once the store has produced a pending order, finish verify/ack/confirm even if the caller
                    // timeout expired. Otherwise consumable/rental purchases can get stuck as already-owned.
                    Debug.LogWarning($"[{Tag}] Purchase cancellation requested after pending order received. Continuing finalize path without caller cancellation.");
                    finalizeCt = CancellationToken.None;
                }

                var store = _purchaseStore.StoreKey;
                var payload = _purchaseStore.BuildVerifyPayload(pendingOrder.Info.Receipt);
                var verifyResult = await verifyPurchaseAsync(internalProductId, storeProductId, kind, store, payload, finalizeCt);
                if (verifyResult.IsFailure)
                {
                    clearCurrentOnExit = false; // keep progress snapshot for recovery retry
                    return CommonResult<PurchaseFinalResult>.Failure(verifyResult.Error!);
                }

                var response = verifyResult.Value!;
                var status = response.ResultStatus;

                if (status == "GRANTED" || status == "ALREADY_GRANTED")
                {
                    purchaseStorage?.MarkVerifyAccepted(response.PurchaseId, response.VerifyStatus);
                    if (status == "ALREADY_GRANTED")
                    {
                        if (recoverClientGrantApplied) purchaseStorage?.MarkClientGrantApplied();
                        if (recoverClientGrantReported) purchaseStorage?.MarkClientGrantReported();
                    }

                    var clientGrantStatus = string.IsNullOrEmpty(response.ClientGrantStatus)
                        ? "PENDING"
                        : response.ClientGrantStatus;
                    var storeConfirmStatus = string.IsNullOrEmpty(response.StoreConfirmStatus)
                        ? "PENDING"
                        : response.StoreConfirmStatus;
                    purchaseStorage?.UpsertRefundSupportLog(
                        response.PurchaseId,
                        internalProductId,
                        purchaseKindString,
                        _purchaseStore.StoreKey,
                        response.VerifyStatus,
                        clientGrantStatus,
                        storeConfirmStatus);

                    if (storeConfirmStatus == "CONFIRMED" || recoverStoreConfirmedLocal)
                        purchaseStorage?.MarkStoreConfirmedLocal();

                    if (storeConfirmStatus != "CONFIRMED")
                    {
                        try
                        {
                            _controller.ConfirmPurchase(pendingOrder);
                            purchaseStorage?.MarkStoreConfirmedLocal();
                        }
                        catch (Exception ex)
                        {
                            clearCurrentOnExit = false;
                            return CommonResult<PurchaseFinalResult>.Failure(
                                COMMON_ERROR_TYPE.PURCHASE_STORE_FAILED,
                                $"ConfirmPurchase failed: {ex.Message}");
                        }

                        if (!string.IsNullOrEmpty(response.PurchaseId))
                        {
                            var confirmAck = await ackPurchaseStoreConfirmAsync(response.PurchaseId, finalizeCt);
                            if (confirmAck.IsFailure)
                            {
                                clearCurrentOnExit = false;
                                return CommonResult<PurchaseFinalResult>.Failure(confirmAck.Error!);
                            }

                            storeConfirmStatus = "CONFIRMED";
                            purchaseStorage?.UpsertRefundSupportLog(
                                response.PurchaseId,
                                internalProductId,
                                purchaseKindString,
                                _purchaseStore.StoreKey,
                                response.VerifyStatus,
                                clientGrantStatus,
                                storeConfirmStatus);
                        }
                    }

                    var rewardGroupId = ResolveRewardGroupId(internalProductId);
                    var rewards = Array.Empty<RewardData>();
                    var needsClientGrantDelivery = status == "GRANTED";
                    if (!needsClientGrantDelivery && status == "ALREADY_GRANTED" &&
                        (clientGrantStatus == "PENDING" || clientGrantStatus == "FAILED_REPORTED"))
                    {
                        needsClientGrantDelivery = !(purchaseStorage?.Current.ClientGrantApplied ?? false);
                    }

                    if (needsClientGrantDelivery)
                    {
                        rewards = ResolveRewardDatas(rewardGroupId);
                        clearCurrentOnExit = false; // caller must ack/report client grant result
                    }
                    else
                    {
                        purchaseStorage?.ClearCurrent();
                        clearCurrentOnExit = false;
                    }

                    return CommonResult<PurchaseFinalResult>.Success(
                        new PurchaseFinalResult(
                            response.PurchaseId,
                            internalProductId,
                            kind,
                            status,
                            rewardGroupId,
                            rewards,
                            clientGrantStatus,
                            needsClientGrantDelivery));
                }

                var rejectReason = response.RejectReason;

                // 취소된 구매(purchaseState=1) 감지 → ConfirmPurchase로 스토어에서 제거
                if (!string.IsNullOrEmpty(rejectReason) &&
                    rejectReason.Contains("purchaseState=1") &&
                    pendingOrder.Info != null)
                {
                    Debug.Log($"[{Tag}] Cancelled purchase detected (purchaseState=1). " +
                              $"Consuming to clear from store. storeProductId={storeProductId}");
                    try
                    {
                        _controller.ConfirmPurchase(pendingOrder);
                        Debug.Log($"[{Tag}] ConfirmPurchase succeeded for cancelled purchase. storeProductId={storeProductId}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[{Tag}] ConfirmPurchase for cancelled purchase failed: {ex.Message}");
                    }
                    // 스토어에서 제거 완료 → current 클리어하여 재구매 가능하게 함
                    purchaseStorage?.ClearCurrent();
                    clearCurrentOnExit = false;
                    return CommonResult<PurchaseFinalResult>.Failure(
                        COMMON_ERROR_TYPE.PURCHASE_VERIFY_REJECTED_UNKNOWN,
                        $"{status}:{rejectReason} (cancelled purchase consumed, retry possible)");
                }

                // 재시도 대상: 스토어 API 일시적 오류(네트워크/타임아웃)만 해당.
                // STORE_VERIFY_MISSING_PURCHASE_TIME, STORE_VERIFY_PARSE_ERROR_*는 동일 영수증으로
                // 재시도해도 결과가 동일하므로 즉시 소비+클리어한다.
                var isRecoverableRejection =
                    status == "PENDING" ||
                    (status == "REJECTED" && rejectReason == "STORE_VERIFY_ERROR");

                var retryCount = purchaseStorage?.Current.VerifyRetryCount ?? 0;
                var keepCurrentForRecovery = isRecoverableRejection && retryCount < MaxVerifyRecoveryRetries;

                if (isRecoverableRejection)
                    purchaseStorage?.IncrementVerifyRetryCount();

                if (isRecoverableRejection && !keepCurrentForRecovery)
                {
                    Debug.LogWarning($"[{Tag}] Verify recovery retry limit exceeded ({MaxVerifyRecoveryRetries}). " +
                        $"Consuming pending order and clearing current. rejectReason={rejectReason}");
                }

                if (keepCurrentForRecovery)
                    clearCurrentOnExit = false;

                // 영구 거부 또는 재시도 상한 초과 → pending order를 ConfirmPurchase로 스토어에서 제거하여
                // Consumable/Rental의 보상 없는 소비, NonConsumable의 영구 already-owned를 방지한다.
                if (!keepCurrentForRecovery && pendingOrder.Info != null)
                {
                    try
                    {
                        Debug.Log($"[{Tag}] Permanently rejected purchase. Consuming pending order to clear from store. storeProductId={storeProductId} reason={rejectReason}");
                        _controller.ConfirmPurchase(pendingOrder);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[{Tag}] ConfirmPurchase for rejected purchase failed: {ex.Message}");
                    }
                }

                COMMON_ERROR_TYPE errorType;
                if (rejectReason == "SEASON_PASS_ALREADY_OWNED")
                    errorType = COMMON_ERROR_TYPE.PURCHASE_SEASON_PASS_ALREADY_OWNED;
                else
                    errorType = COMMON_ERROR_TYPE.PURCHASE_VERIFY_REJECTED_UNKNOWN;

                return CommonResult<PurchaseFinalResult>.Failure(
                    errorType, $"{status}:{rejectReason}");
            }
            finally
            {
                _purchaseInProgress = false;
                if (clearCurrentOnExit)
                    purchaseStorage?.ClearCurrent();
            }
        }

        async Task<CommonResult<PurchaseFinalResult>> resumeAfterStoreConfirmAsync(
            string internalProductId,
            PurchaseKind kind,
            CancellationToken ct)
        {
            var purchaseStorage = getPurchaseStorageOrNull();
            if (purchaseStorage == null)
                return CommonResult<PurchaseFinalResult>.Failure(
                    COMMON_ERROR_TYPE.PURCHASE_INTERRUPTED_STORAGE_UNAVAILABLE,
                    "PurchaseStorage is not available.");

            var current = purchaseStorage.Current;
            if (!current.IsPurchaseInProgress || !current.StoreConfirmedLocal || string.IsNullOrEmpty(current.PurchaseId))
                return CommonResult<PurchaseFinalResult>.Failure(
                    COMMON_ERROR_TYPE.PURCHASE_INTERRUPTED_SNAPSHOT_KIND_INVALID,
                    "Interrupted purchase is not resumable after store confirm.");

            var purchaseKindString = PurchaseKindToString(kind);
            var verifyStatus = string.IsNullOrEmpty(current.VerifyStatus) ? "GRANTED" : current.VerifyStatus;
            var clientGrantStatus = current.ClientGrantReported ? "APPLIED_ACKED" : "PENDING";
            var storeConfirmStatus = "PENDING";
            if (purchaseStorage.TryGetRefundSupportLog(current.PurchaseId, out var log))
            {
                if (!string.IsNullOrEmpty(log.VerifyStatus))
                    verifyStatus = log.VerifyStatus;
                if (!string.IsNullOrEmpty(log.ClientGrantStatus))
                    clientGrantStatus = log.ClientGrantStatus;
                if (!string.IsNullOrEmpty(log.StoreConfirmStatus))
                    storeConfirmStatus = log.StoreConfirmStatus;
            }

            if (storeConfirmStatus != "CONFIRMED")
            {
                var confirmAck = await ackPurchaseStoreConfirmAsync(current.PurchaseId, ct);
                if (confirmAck.IsFailure)
                    return CommonResult<PurchaseFinalResult>.Failure(confirmAck.Error!);

                storeConfirmStatus = "CONFIRMED";
                purchaseStorage.UpsertRefundSupportLog(
                    current.PurchaseId,
                    internalProductId,
                    purchaseKindString,
                    current.StoreKey,
                    verifyStatus,
                    clientGrantStatus,
                    storeConfirmStatus);
            }

            var rewardGroupId = ResolveRewardGroupId(internalProductId);
            var rewards = Array.Empty<RewardData>();
            var needsClientGrantDelivery =
                !purchaseStorage.Current.ClientGrantApplied &&
                (clientGrantStatus == "PENDING" || clientGrantStatus == "FAILED_REPORTED");
            if (needsClientGrantDelivery)
            {
                rewards = ResolveRewardDatas(rewardGroupId);
            }
            else
            {
                // 보상 적용 완료(ClientGrantApplied)이지만 서버 ACK 미완료(ClientGrantReported=false)인 경우,
                // 서버에 APPLIED_ACKED를 보고하여 clientGrantStatus를 확정한다.
                if (purchaseStorage.Current.ClientGrantApplied && !purchaseStorage.Current.ClientGrantReported)
                {
                    var ackResult = await reportPurchaseClientGrantResultAsync(current.PurchaseId, "APPLIED_ACKED", ct);
                    if (ackResult.IsSuccess)
                    {
                        purchaseStorage.MarkClientGrantReported();
                        clientGrantStatus = "APPLIED_ACKED";
                        purchaseStorage.UpsertRefundSupportLog(
                            current.PurchaseId,
                            internalProductId,
                            purchaseKindString,
                            current.StoreKey,
                            verifyStatus,
                            clientGrantStatus,
                            storeConfirmStatus);
                    }
                }

                purchaseStorage.ClearCurrent();
            }

            return CommonResult<PurchaseFinalResult>.Success(
                new PurchaseFinalResult(
                    current.PurchaseId,
                    internalProductId,
                    kind,
                    "ALREADY_GRANTED",
                    rewardGroupId,
                    rewards,
                    clientGrantStatus,
                    needsClientGrantDelivery));
        }

        async Task<CommonResult<VerifyPurchaseResponse>> verifyPurchaseAsync(
            string internalProductId, string storeProductId, PurchaseKind kind, string store, string payload,
            CancellationToken ct)
        {
            var data = new Dictionary<string, object>
            {
                ["storeKey"] = store,
                ["internalProductId"] = internalProductId,
                ["storeProductId"] = storeProductId,
#if UNITY_ANDROID
                ["packageName"] = Application.identifier,
#endif
                ["kind"] = PurchaseKindToString(kind),
                ["payload"] = payload,
            };

            // Rental / SeasonPass: REWARD table Id를 서버 entitlements 키로 전달
            if (kind == PurchaseKind.Rental || kind == PurchaseKind.SeasonPass)
            {
                var rgId = ResolveRewardGroupId(internalProductId);
                var rewards = ResolveRewardDatas(rgId);
                for (int i = 0; i < rewards.Length; i++)
                {
                    if (kind == PurchaseKind.Rental && rewards[i].Type == REWARD_TYPE.RENTAL)
                    {
                        data["rentalId"] = rewards[i].Id;
                        break;
                    }
                    if (kind == PurchaseKind.SeasonPass && rewards[i].Type == REWARD_TYPE.PASS)
                    {
                        data["seasonPassId"] = rewards[i].Id;
                        break;
                    }
                }
            }

            var result = await FirebaseCallableManager.Instance.VerifyPurchaseAsync(data, ct);
            if (result.IsFailure)
                return result;

            var verifyResponse = result.Value!;

            // FirebaseCallableManager가 반환한 EntitlementsSnapshot은 raw season pass ID를 사용한다.
            // domain 변환(ResolveSeasonPassId) 후 캐시한다.
            if (verifyResponse.Snapshot.HasValue)
            {
                var resolved = resolveEntitlementsSeasonPasses(verifyResponse.Snapshot.Value);
                cacheEntitlementsSnapshot(resolved);
            }

            return result;
        }

        async Task<CommonResult> completePurchaseClientGrantAsync(string purchaseId, string clientGrantStatus, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(purchaseId))
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.PURCHASE_CLIENT_GRANT_PURCHASE_ID_EMPTY,
                    "purchaseId is required.");
            }

            var report = await reportPurchaseClientGrantResultAsync(purchaseId, clientGrantStatus, ct);
            if (report.IsFailure)
                return report;

            var purchaseStorage = getPurchaseStorageOrNull();
            if (purchaseStorage == null)
                return CommonResult.Ok();

            var current = purchaseStorage.Current;
            var isCurrent =
                current.IsPurchaseInProgress &&
                string.Equals(current.PurchaseId, purchaseId, StringComparison.Ordinal);
            var isAppliedAcked = clientGrantStatus == "APPLIED_ACKED";

            if (isCurrent && isAppliedAcked)
            {
                purchaseStorage.MarkClientGrantApplied();
                purchaseStorage.MarkClientGrantReported();
            }

            var internalProductId = string.Empty;
            var kind = string.Empty;
            var storeKey = string.Empty;
            var verifyStatus = string.Empty;
            var storeConfirmStatus = string.Empty;

            if (purchaseStorage.TryGetRefundSupportLog(purchaseId, out var log))
            {
                internalProductId = log.InternalProductId;
                kind = log.Kind;
                storeKey = log.StoreKey;
                verifyStatus = log.VerifyStatus;
                storeConfirmStatus = log.StoreConfirmStatus;
            }
            else if (isCurrent)
            {
                internalProductId = current.InternalProductId;
                kind = current.Kind;
                storeKey = current.StoreKey;
                verifyStatus = current.VerifyStatus;
                storeConfirmStatus = current.StoreConfirmedLocal ? "CONFIRMED" : "PENDING";
            }

            purchaseStorage.UpsertRefundSupportLog(
                purchaseId,
                internalProductId,
                kind,
                storeKey,
                string.IsNullOrEmpty(verifyStatus) ? "GRANTED" : verifyStatus,
                clientGrantStatus,
                string.IsNullOrEmpty(storeConfirmStatus) ? "CONFIRMED" : storeConfirmStatus);

            // APPLIED_ACKED: grant 성공 완료 → current 클리어
            // FAILED_REPORTED: grant 최종 실패 → current 클리어하여 재구매 차단 방지
            if (isCurrent)
                purchaseStorage.ClearCurrent();

            return CommonResult.Ok();
        }

        async Task<CommonResult> reportPurchaseClientGrantResultAsync(string purchaseId, string clientGrantStatus, CancellationToken ct)
        {
            var data = new Dictionary<string, object>
            {
                ["purchaseId"] = purchaseId,
                ["clientGrantStatus"] = clientGrantStatus,
            };

            return await FirebaseCallableManager.Instance.AckPurchaseClientGrantAsync(data, ct);
        }

        async Task<CommonResult> ackPurchaseStoreConfirmAsync(string purchaseId, CancellationToken ct)
        {
            var data = new Dictionary<string, object>
            {
                ["purchaseId"] = purchaseId,
            };

            return await FirebaseCallableManager.Instance.AckPurchaseStoreConfirmAsync(data, ct);
        }

        async Task<CommonResult> ackRefundAppliedAsync(string purchaseId, CancellationToken ct)
        {
            var data = new Dictionary<string, object>
            {
                ["purchaseId"] = purchaseId,
            };

            return await FirebaseCallableManager.Instance.AckRefundAppliedAsync(data, ct);
        }

        // ── Store Fetch Helpers ────────────────────────────────────

        /// <summary>
        /// FetchPurchases()를 호출하여 스토어의 미소비/미확인 구매 목록을 가져온다.
        /// OnPurchasePending 콜백에 의존하지 않고 Orders.PendingOrders를 직접 반환한다.
        /// Unity IAP의 WasPurchaseAlreadyProcessed가 동일 세션 내 재처리를 차단하기 때문에,
        /// OnPurchasePending 콜백이 발생하지 않을 수 있으므로 직접 접근이 필수.
        /// </summary>
        async Task<CommonResult<Orders>> fetchPendingPurchasesAsync(CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<CommonResult<Orders>>();

            void onFetched(Orders orders)
            {
                var p = orders?.PendingOrders?.Count ?? 0;
                var c = orders?.ConfirmedOrders?.Count ?? 0;
                var d = orders?.DeferredOrders?.Count ?? 0;
                Debug.Log($"[{Tag}] fetchPendingPurchasesAsync.onFetched: pending={p}, confirmed={c}, deferred={d}");
                tcs.TrySetResult(CommonResult<Orders>.Success(orders));
            }
            void onFailed(PurchasesFetchFailureDescription failure)
            {
                Debug.LogWarning($"[{Tag}] fetchPendingPurchasesAsync.onFailed: {failure.Message}");
                tcs.TrySetResult(CommonResult<Orders>.Failure(
                    COMMON_ERROR_TYPE.PURCHASE_PURCHASE_REQUEST_FAILED,
                    $"FetchPurchases failed: {failure.Message}"));
            }

            _controller.OnPurchasesFetched += onFetched;
            _controller.OnPurchasesFetchFailed += onFailed;

            try
            {
                _controller.FetchPurchases();

                if (ct.CanBeCanceled)
                    ct.Register(() => tcs.TrySetCanceled(ct));

                return await tcs.Task;
            }
            finally
            {
                _controller.OnPurchasesFetched -= onFetched;
                _controller.OnPurchasesFetchFailed -= onFailed;
            }
        }

        /// <summary>
        /// Orders.PendingOrders에서 storeProductId가 일치하는 PendingOrder를 직접 찾는다.
        /// WasPurchaseAlreadyProcessed로 인해 OnPurchasePending이 발생하지 않는 경우의 fallback.
        /// </summary>
        static bool TryFindPendingOrderFromFetchedOrders(Orders orders, string storeProductId, out PendingOrder found)
        {
            found = default;
            if (orders?.PendingOrders == null)
            {
                Debug.LogWarning($"[{Tag}] TryFindPendingOrderFromFetchedOrders: orders or PendingOrders is null.");
                return false;
            }

            var pendingCount = orders.PendingOrders.Count;
            var confirmedCount = orders.ConfirmedOrders?.Count ?? 0;
            var deferredCount = orders.DeferredOrders?.Count ?? 0;
            Debug.Log($"[{Tag}] FetchedOrders: pending={pendingCount}, confirmed={confirmedCount}, deferred={deferredCount}, expected={storeProductId}");

            foreach (var pending in orders.PendingOrders)
            {
                // Receipt에서 productId를 추출하여 매칭
                var candidateStoreProductId = TryExtractStoreProductIdFromReceipt(pending.Info.Receipt);
                Debug.Log($"[{Tag}] PendingOrder candidate: extracted={candidateStoreProductId}, transactionID={pending.Info.TransactionID}, receiptLen={pending.Info.Receipt?.Length ?? 0}");
                if (!string.IsNullOrEmpty(candidateStoreProductId) &&
                    string.Equals(candidateStoreProductId, storeProductId, StringComparison.Ordinal))
                {
                    found = pending;
                    return true;
                }
            }

            return false;
        }

        // ── Event Handlers ────────────────────────────────────────

        void onPurchasePending(PendingOrder order)
        {
            getPurchaseStorageOrNull()?.MarkStorePending();
            if (_purchaseTcs != null)
            {
                if (_purchaseTcs.TrySetResult(order))
                    return;
            }

            _deferredPendingOrders.Add(order);
            var storeProductId = TryExtractStoreProductIdFromReceipt(order.Info.Receipt);
            Debug.LogWarning($"[{Tag}] Deferred pending purchase queued. storeProductId={storeProductId}");
        }

        void onPurchaseFailed(FailedOrder order)
        {
            _purchaseTcs?.TrySetException(new Exception(order.Details ?? order.FailureReason.ToString()));
        }

        void onStoreDisconnected(StoreConnectionFailureDescription desc)
        {
            Debug.LogWarning($"[{Tag}] Store disconnected: {desc}");
        }

        protected override void onDestroy()
        {
            if (_controller != null)
            {
                _controller.OnPurchasePending -= onPurchasePending;
                _controller.OnPurchaseFailed -= onPurchaseFailed;
                _controller.OnStoreDisconnected -= onStoreDisconnected;
            }
        }

        // ── Catalog Helpers ────────────────────────────────────────

        static ProductType toUnityProductType(PurchaseProductType type)
        {
            switch (type)
            {
                case PurchaseProductType.Consumable: return ProductType.Consumable;
                case PurchaseProductType.Subscription: return ProductType.Subscription;
                case PurchaseProductType.NonConsumable:
                default: return ProductType.NonConsumable;
            }
        }

#else
        // ── Unity Purchasing unavailable ────────────────────────────

        public void SetPurchaseStore(IPurchaseStore store) { }
        public void SetProductCatalog(IPurchaseProductCatalog catalog) { }

        static readonly Task<CommonResult> _notSupportedInit =
            Task.FromResult(CommonResult.Failure(COMMON_ERROR_TYPE.IAP_NOT_SUPPORTED, "Unity Purchasing not available."));

        static readonly Task<CommonResult<PurchaseFinalResult>> _notSupported =
            Task.FromResult(CommonResult<PurchaseFinalResult>.Failure(COMMON_ERROR_TYPE.IAP_NOT_SUPPORTED, "Unity Purchasing not available."));

        static readonly Task<CommonResult<RecentPurchaseItem>> _notSupportedRecent =
            Task.FromResult(CommonResult<RecentPurchaseItem>.Failure(COMMON_ERROR_TYPE.IAP_NOT_SUPPORTED, "Unity Purchasing not available."));

        static readonly Task<CommonResult<EntitlementsSnapshot>> _notSupportedSnapshot =
            Task.FromResult(CommonResult<EntitlementsSnapshot>.Failure(COMMON_ERROR_TYPE.IAP_NOT_SUPPORTED, "Unity Purchasing not available."));
#if UNITY_EDITOR
        static readonly Task<CommonResult<PurchaseSyncResult>> _notSupportedSync =
            Task.FromResult(CommonResult<PurchaseSyncResult>.Success(CreateNoOpPurchaseSyncResult()));
#else
        static readonly Task<CommonResult<PurchaseSyncResult>> _notSupportedSync =
            Task.FromResult(CommonResult<PurchaseSyncResult>.Failure(COMMON_ERROR_TYPE.IAP_NOT_SUPPORTED, "Unity Purchasing not available."));
#endif
        static readonly Task<CommonResult<long>> _notSupportedLong =
            Task.FromResult(CommonResult<long>.Failure(COMMON_ERROR_TYPE.IAP_NOT_SUPPORTED, "Unity Purchasing not available."));
        static readonly Task<CommonResult<RefundSyncResult>> _notSupportedRefundSync =
            Task.FromResult(CommonResult<RefundSyncResult>.Failure(COMMON_ERROR_TYPE.IAP_NOT_SUPPORTED, "Unity Purchasing not available."));

        public Task<CommonResult> InitializeAsync(CancellationToken ct = default) => _notSupportedInit;
        public Task<CommonResult<PurchaseSyncResult>> SyncAsync(
            SessionInitSnapshot? initSnapshot = null, CancellationToken ct = default) => _notSupportedSync;
        public Task<CommonResult<PurchaseFinalResult>> PurchaseAsync(string internalProductId, CancellationToken ct = default) => _notSupported;
#if UNITY_EDITOR
        static readonly Task<CommonResult<RetryInterruptedPurchaseResult>> _notSupportedRetryInterrupted =
            Task.FromResult(CommonResult<RetryInterruptedPurchaseResult>.Success(CreateNoOpRetryInterruptedPurchaseResult()));
#else
        static readonly Task<CommonResult<RetryInterruptedPurchaseResult>> _notSupportedRetryInterrupted =
            Task.FromResult(CommonResult<RetryInterruptedPurchaseResult>.Failure(COMMON_ERROR_TYPE.IAP_NOT_SUPPORTED, "Unity Purchasing not available."));
#endif

        public Task<CommonResult<RetryInterruptedPurchaseResult>> RetryInterruptedPurchaseAsync(CancellationToken ct = default)
            => _notSupportedRetryInterrupted;
        public Task<CommonResult> AckPurchaseClientGrantAppliedAsync(string purchaseId, CancellationToken ct = default) => _notSupportedInit;
        public Task<CommonResult> ReportPurchaseClientGrantFailureAsync(string purchaseId, CancellationToken ct = default) => _notSupportedInit;
        public Task<CommonResult<EntitlementsSnapshot>> RestoreAsync(CancellationToken ct = default) => _notSupportedSnapshot;
        public Task<CommonResult<EntitlementsSnapshot>> SyncEntitlementsAsync(CancellationToken ct = default) => _notSupportedSnapshot;
        public Task<CommonResult<long>> GetRentalRemainingMsAsync(string internalProductId, CancellationToken ct = default) => _notSupportedLong;
        public Task<CommonResult<RecentPurchaseItem>> GetLatestConsumablePurchase30dAsync(CancellationToken ct = default) => _notSupportedRecent;
        async Task<CommonResult<RefundSyncResult>> syncRefundsPageAsync(string cursor = null, int pageSize = 50, CancellationToken ct = default)
            => await _notSupportedRefundSync;
#endif

        // ── Helpers ───────────────────────────────────────────────

        /// <summary>
        /// FirebaseCallableManager가 반환한 raw EntitlementsSnapshot의 season pass ID를
        /// ResolveSeasonPassId로 변환한 새 snapshot을 반환한다.
        /// </summary>
        static EntitlementsSnapshot resolveEntitlementsSeasonPasses(EntitlementsSnapshot raw)
        {
            var seasonPasses = new List<string>();
            var seasonPassSet = new HashSet<string>(StringComparer.Ordinal);
            if (raw.OwnedSeasonPasses != null)
            {
                for (var i = 0; i < raw.OwnedSeasonPasses.Count; i++)
                {
                    var resolved = ResolveSeasonPassId(raw.OwnedSeasonPasses[i]);
                    if (!string.IsNullOrEmpty(resolved) && seasonPassSet.Add(resolved))
                        seasonPasses.Add(resolved);
                }
            }

            return new EntitlementsSnapshot(seasonPasses, raw.CurrencyBalances, raw.Rentals, raw.ServerNowUtcMs);
        }

        /// <summary>
        /// FirebaseCallableManager가 반환한 RefundPageResult의 raw item을
        /// domain 보강(rewardGroupId, rewards, parsed kind) 후 RefundSyncResult로 변환한다.
        /// </summary>
        static RefundSyncResult enrichAdjustmentItems(RefundPageResult page)
        {
            var items = new List<PurchaseAdjustmentResult>();
            for (var i = 0; i < page.Items.Length; i++)
            {
                var raw = page.Items[i];
                var rewardGroupId = ResolveRewardGroupId(raw.InternalProductId);
                var rewards = ResolveRewardDatas(rewardGroupId);

                PurchaseKind? kind = null;
                if (TryParseStoredPurchaseKind(raw.KindString, out var parsedKind))
                    kind = parsedKind;

                items.Add(new PurchaseAdjustmentResult(
                    raw.PurchaseId,
                    raw.InternalProductId,
                    kind,
                    raw.ResultStatus,
                    rewardGroupId,
                    rewards,
                    raw.Reason,
                    raw.UpdatedAtUtcMs));
            }

            return new RefundSyncResult(items.ToArray(), page.NextCursor, page.HasMore);
        }

        static void cacheEntitlementsSnapshot(EntitlementsSnapshot snapshot)
        {
            var inventoryManager = getInventoryManagerOrNull();
            var inventory = inventoryManager?.Storage;
            if (inventoryManager == null || inventory == null)
                return;

            // serverNowUtcMs 기반으로 expiresAtServerUtcMs → expiresAtClientUtcMs 변환
            var serverNow = snapshot.ServerNowUtcMs > 0
                ? snapshot.ServerNowUtcMs
                : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var clientNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var clockDelta = clientNow - serverNow;

            // 서버 snapshot에 없는 로컬 rental 제거 (환불 등으로 서버에서 사라진 경우)
            var localRentalKeys = new List<string>(inventory.Rentals.Keys);
            for (int i = 0; i < localRentalKeys.Count; i++)
            {
                if (!snapshot.Rentals.ContainsKey(localRentalKeys[i]))
                    inventory.RemoveRental(localRentalKeys[i]);
            }

            foreach (var kv in snapshot.Rentals)
            {
                var expiresAtClientUtcMs = kv.Value + clockDelta;
                inventory.SetRental(kv.Key, expiresAtClientUtcMs);
            }

            // 서버 snapshot에 없는 로컬 seasonPass 제거 (환불 등으로 서버에서 사라진 경우)
            var serverSeasonPassSet = new HashSet<string>(snapshot.OwnedSeasonPasses);
            var localSeasonPassKeys = new List<string>(inventory.Passes.Keys);
            for (int i = 0; i < localSeasonPassKeys.Count; i++)
            {
                if (!serverSeasonPassSet.Contains(localSeasonPassKeys[i]))
                    inventoryManager.RemovePassOwnership(localSeasonPassKeys[i]);
            }

            foreach (var id in snapshot.OwnedSeasonPasses)
            {
                inventoryManager.SetPassOwnership(id, true);
            }
        }

        static InventoryManager getInventoryManagerOrNull()
        {
            try
            {
                return InventoryManager.Instance;
            }
            catch
            {
                return null;
            }
        }

        static PurchaseKind ProductKindToPurchaseKind(PURCHASE_KIND kind)
        {
            switch (kind)
            {
                case PURCHASE_KIND.CONSUMABLE: return PurchaseKind.Consumable;
                case PURCHASE_KIND.RENTAL: return PurchaseKind.Rental;
                case PURCHASE_KIND.SUBSCRIPTION: return PurchaseKind.Subscription;
                case PURCHASE_KIND.PASS: return PurchaseKind.SeasonPass;
                default: return PurchaseKind.Consumable;
            }
        }

        static string PurchaseKindToString(PurchaseKind kind)
        {
            switch (kind)
            {
                case PurchaseKind.Consumable: return "Consumable";
                case PurchaseKind.Rental: return "Rental";
                case PurchaseKind.Subscription: return "Subscription";
                case PurchaseKind.SeasonPass: return "SeasonPass";
                default: return "Consumable";
            }
        }

        static bool TryParseStoredPurchaseKind(string value, out PurchaseKind kind)
        {
            switch (value)
            {
                case "Consumable":
                    kind = PurchaseKind.Consumable;
                    return true;
                case "Rental":
                    kind = PurchaseKind.Rental;
                    return true;
                case "Subscription":
                    kind = PurchaseKind.Subscription;
                    return true;
                case "SeasonPass":
                    kind = PurchaseKind.SeasonPass;
                    return true;
                default:
                    kind = PurchaseKind.Consumable;
                    return false;
            }
        }

        static bool TryResolveInterruptedPurchaseKind(string internalProductId, string storedKind, out PurchaseKind kind)
        {
            if (TryParseStoredPurchaseKind(storedKind, out kind))
                return true;

            var product = TB_PURCHASE.Get(internalProductId);
            if (product == null)
            {
                kind = PurchaseKind.Consumable;
                return false;
            }

            kind = ProductKindToPurchaseKind(product.Kind);
            return true;
        }

        static async Task<CommonResult<bool>> ensurePurchaseLoginReadyAsync(CancellationToken ct)
        {
            try
            {
                var loginManager = LoginManager.Instance;
                if (loginManager == null)
                    return CommonResult<bool>.Success(false);

                return await loginManager.EnsurePurchaseLoginReadyAsync(ct);
            }
            catch
            {
                return CommonResult<bool>.Success(false);
            }
        }

        bool TryTakeDeferredPendingOrder(string expectedStoreProductId, out PendingOrder pendingOrder)
        {
            for (var i = 0; i < _deferredPendingOrders.Count; i++)
            {
                var candidate = _deferredPendingOrders[i];
                var candidateStoreProductId = TryExtractStoreProductIdFromReceipt(candidate.Info.Receipt);

                if (!string.IsNullOrEmpty(candidateStoreProductId))
                {
                    if (!string.Equals(candidateStoreProductId, expectedStoreProductId, StringComparison.Ordinal))
                        continue;
                }
                else if (_deferredPendingOrders.Count != 1)
                {
                    continue;
                }

                _deferredPendingOrders.RemoveAt(i);
                pendingOrder = candidate;
                return true;
            }

            pendingOrder = default;
            return false;
        }

        /// <summary>
        /// Receipt 문자열에서 productId를 추출한다.
        /// Unity IAP v6 Google Play receipt은 UnifiedReceipt 래핑(이중 JSON 이스케이프)으로 인해
        /// productId가 다양한 이스케이프 깊이로 존재한다.
        /// 이스케이프 레벨과 무관하게 "productId" 키워드를 찾은 후,
        /// 그 뒤에 오는 SKU 형태의 값(알파벳/숫자/점/밑줄/하이픈)을 직접 추출한다.
        /// </summary>
        static string TryExtractStoreProductIdFromReceipt(string receipt)
        {
            if (string.IsNullOrEmpty(receipt))
                return string.Empty;

            // "productId" 문자열을 찾는다 (이스케이프 관계없이 리터럴 텍스트 "productId"는 항상 존재)
            const string keyword = "productId";
            var searchStart = 0;
            while (searchStart < receipt.Length)
            {
                var kwIndex = receipt.IndexOf(keyword, searchStart, StringComparison.Ordinal);
                if (kwIndex < 0)
                    break;

                // productId 뒤의 구분자(이스케이프된 따옴표, 콜론, 공백 등)를 건너뛰고
                // SKU 값 시작점을 찾는다.
                var afterKeyword = kwIndex + keyword.Length;
                var valueStart = -1;
                for (var i = afterKeyword; i < receipt.Length && i < afterKeyword + 20; i++)
                {
                    var c = receipt[i];
                    // SKU에 유효한 첫 문자를 만나면 값 시작
                    if (char.IsLetterOrDigit(c))
                    {
                        valueStart = i;
                        break;
                    }
                    // 구분자 문자 (따옴표, 역슬래시, 콜론, 공백) 는 건너뜀
                    if (c == '"' || c == '\\' || c == ':' || c == ' ')
                        continue;
                    // 예상 외 문자면 이 위치의 productId는 아님
                    break;
                }

                if (valueStart < 0)
                {
                    searchStart = afterKeyword;
                    continue;
                }

                // SKU 값 추출: 알파벳, 숫자, 점, 밑줄, 하이픈만 허용
                var valueEnd = valueStart;
                for (var i = valueStart; i < receipt.Length; i++)
                {
                    var c = receipt[i];
                    if (char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-')
                    {
                        valueEnd = i + 1;
                        continue;
                    }
                    break;
                }

                if (valueEnd > valueStart)
                {
                    var extracted = receipt.Substring(valueStart, valueEnd - valueStart);
                    // 최소 길이 검증 (너무 짧은 값은 무시)
                    if (extracted.Length >= 3)
                        return extracted;
                }

                searchStart = afterKeyword;
            }

            return string.Empty;
        }

        static bool isRepurchasableStoreKind(PurchaseKind kind)
        {
            return kind == PurchaseKind.Consumable || kind == PurchaseKind.Rental;
        }

        static bool isStoreAlreadyOwnedFailureMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return false;

            var lower = message.ToLowerInvariant();
            return lower.Contains("already owned") ||
                   lower.Contains("item_already_owned") ||
                   lower.Contains("item already owned") ||
                   lower.Contains("already_owned") ||
                   lower.Contains("이미 구매") ||
                   lower.Contains("이미 보유");
        }


        static PurchaseStorage getPurchaseStorageOrNull()
        {
            try
            {
                var purchaseManager = PurchaseManager.Instance;
                return purchaseManager != null ? purchaseManager.Storage : null;
            }
            catch
            {
                return null;
            }
        }

        // ── Data Types ─────────────────────────────────────────────

        public readonly struct VerifyPurchaseRequest
        {
            public VerifyPurchaseRequest(string internalProductId, PurchaseKind kind, string store, string payload)
            {
                InternalProductId = internalProductId;
                Kind = kind;
                Store = store;
                Payload = payload;
            }

            public string InternalProductId { get; }
            public PurchaseKind Kind { get; }
            public string Store { get; }
            public string Payload { get; }
        }


        public readonly struct PurchaseFinalResult
        {
            public PurchaseFinalResult(
                string internalProductId,
                PurchaseKind kind,
                string resultStatus,
                string rewardGroupId,
                RewardData[] appliedRewards)
                : this(string.Empty, internalProductId, kind, resultStatus, rewardGroupId, appliedRewards, string.Empty, false)
            {
            }

            public PurchaseFinalResult(
                string purchaseId,
                string internalProductId,
                PurchaseKind kind,
                string resultStatus,
                string rewardGroupId,
                RewardData[] appliedRewards,
                string clientGrantStatus,
                bool needsClientGrantDelivery)
            {
                PurchaseId = purchaseId ?? string.Empty;
                InternalProductId = internalProductId;
                Kind = kind;
                ResultStatus = resultStatus;
                RewardGroupId = rewardGroupId ?? string.Empty;
                AppliedRewards = appliedRewards ?? Array.Empty<RewardData>();
                ClientGrantStatus = clientGrantStatus ?? string.Empty;
                NeedsClientGrantDelivery = needsClientGrantDelivery;
            }

            public string PurchaseId { get; }
            public string InternalProductId { get; }
            public PurchaseKind Kind { get; }
            public string ResultStatus { get; }
            public string RewardGroupId { get; }
            public RewardData[] AppliedRewards { get; }
            public string ClientGrantStatus { get; }
            public bool NeedsClientGrantDelivery { get; }
            public RewardData[] Rewards => AppliedRewards;
        }

        public readonly struct PurchaseSyncResult
        {
            public PurchaseSyncResult(
                RetryInterruptedPurchaseResult? retryInterruptedPurchase,
                CommonError retryInterruptedError,
                RefundResult? refund,
                CommonError refundError,
                EntitlementsSnapshot? entitlements,
                CommonError entitlementsError,
                CommonError saveError)
            {
                RetryInterruptedPurchase = retryInterruptedPurchase;
                RetryInterruptedError = retryInterruptedError;
                Refund = refund;
                RefundError = refundError;
                Entitlements = entitlements;
                EntitlementsError = entitlementsError;
                SaveError = saveError;
            }

            public RetryInterruptedPurchaseResult? RetryInterruptedPurchase { get; }
            public CommonError RetryInterruptedError { get; }
            public RefundResult? Refund { get; }
            public CommonError RefundError { get; }
            public EntitlementsSnapshot? Entitlements { get; }
            public CommonError EntitlementsError { get; }
            public CommonError SaveError { get; }
            public bool HasNonFatalIssues =>
                RetryInterruptedError != null ||
                RefundError != null ||
                EntitlementsError != null ||
                SaveError != null;
        }

        public readonly struct RetryInterruptedPurchaseResult
        {
            public RetryInterruptedPurchaseResult(
                RetryInterruptedPurchaseStatus status,
                string internalProductId,
                PurchaseKind? kind,
                string resultStatus,
                string rewardGroupId,
                RewardData[] appliedRewards)
                : this(status, string.Empty, internalProductId, kind, resultStatus, rewardGroupId, appliedRewards, string.Empty, false)
            {
            }

            public RetryInterruptedPurchaseResult(
                RetryInterruptedPurchaseStatus status,
                string purchaseId,
                string internalProductId,
                PurchaseKind? kind,
                string resultStatus,
                string rewardGroupId,
                RewardData[] appliedRewards,
                string clientGrantStatus,
                bool needsClientGrantDelivery)
            {
                Status = status;
                PurchaseId = purchaseId ?? string.Empty;
                InternalProductId = internalProductId ?? string.Empty;
                Kind = kind;
                ResultStatus = resultStatus ?? string.Empty;
                RewardGroupId = rewardGroupId ?? string.Empty;
                AppliedRewards = appliedRewards ?? Array.Empty<RewardData>();
                ClientGrantStatus = clientGrantStatus ?? string.Empty;
                NeedsClientGrantDelivery = needsClientGrantDelivery;
            }

            public RetryInterruptedPurchaseStatus Status { get; }
            public string PurchaseId { get; }
            public string InternalProductId { get; }
            public PurchaseKind? Kind { get; }
            public string ResultStatus { get; }
            public string RewardGroupId { get; }
            public RewardData[] AppliedRewards { get; }
            public string ClientGrantStatus { get; }
            public bool NeedsClientGrantDelivery { get; }
            public RewardData[] Rewards => AppliedRewards;
        }

        readonly struct PurchaseAdjustmentResult
        {
            public PurchaseAdjustmentResult(
                string purchaseId,
                string internalProductId,
                PurchaseKind? kind,
                string resultStatus,
                string rewardGroupId,
                RewardData[] rewards,
                string reason,
                long updatedAtUtcMs)
            {
                PurchaseId = purchaseId ?? string.Empty;
                InternalProductId = internalProductId ?? string.Empty;
                Kind = kind;
                ResultStatus = resultStatus ?? string.Empty;
                RewardGroupId = rewardGroupId ?? string.Empty;
                Rewards = rewards ?? Array.Empty<RewardData>();
                Reason = reason ?? string.Empty;
                UpdatedAtUtcMs = updatedAtUtcMs;
            }

            public string PurchaseId { get; }
            public string InternalProductId { get; }
            public PurchaseKind? Kind { get; }
            public string ResultStatus { get; }
            public string RewardGroupId { get; }
            public RewardData[] Rewards { get; }
            public string Reason { get; }
            public long UpdatedAtUtcMs { get; }
        }

        readonly struct RefundSyncResult
        {
            public RefundSyncResult(PurchaseAdjustmentResult[] items, string nextCursor, bool hasMore)
            {
                Items = items ?? Array.Empty<PurchaseAdjustmentResult>();
                NextCursor = nextCursor ?? string.Empty;
                HasMore = hasMore;
            }

            public PurchaseAdjustmentResult[] Items { get; }
            public string NextCursor { get; }
            public bool HasMore { get; }
        }

        public readonly struct RefundResult
        {
            public RefundResult(
                int pageCount,
                int handledAdjustmentCount,
                int inventoryAppliedAdjustmentCount,
                int noOpAdjustmentCount,
                int skippedAdjustmentCount,
                int ackFailedCount,
                string cursor)
            {
                PageCount = pageCount;
                HandledAdjustmentCount = handledAdjustmentCount;
                InventoryAppliedAdjustmentCount = inventoryAppliedAdjustmentCount;
                NoOpAdjustmentCount = noOpAdjustmentCount;
                SkippedAdjustmentCount = skippedAdjustmentCount;
                AckFailedCount = ackFailedCount;
                Cursor = cursor ?? string.Empty;
            }

            public int PageCount { get; }
            public int HandledAdjustmentCount { get; }
            public int InventoryAppliedAdjustmentCount { get; }
            public int NoOpAdjustmentCount { get; }
            public int SkippedAdjustmentCount { get; }
            public int AckFailedCount { get; }
            public string Cursor { get; }
        }

        public enum RetryInterruptedPurchaseStatus
        {
            SkippedNoCurrent = 0,
            Retried = 1,
        }

        public enum PurchaseKind
        {
            Consumable = 0,
            Rental = 1,
            Subscription = 2,
            SeasonPass = 3,
        }

    }
}
