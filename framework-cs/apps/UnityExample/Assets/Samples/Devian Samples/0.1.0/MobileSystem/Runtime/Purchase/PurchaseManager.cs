using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Devian.Domain.Common;
using Devian.Domain.Game;
using Firebase.Functions;

#if UNITY_PURCHASING
using UnityEngine.Purchasing;
#endif

namespace Devian
{
    public sealed class PurchaseManager : CompoSingleton<PurchaseManager>
    {
        const string Tag = "PurchaseManager";

        string _functionsRegion = "asia-northeast3";

        protected override void Awake()
        {
            base.Awake();
            SetProductCatalog(new GameProductCatalog());
            SetPurchaseStore(CreateDefaultStore());
        }

        /// <summary>
        /// Firebase Cloud Functions 리전을 설정한다.
        /// 설정하지 않으면 기본 리전(us-central1)을 사용한다.
        /// </summary>
        public void SetFunctionsRegion(string region)
        {
            _functionsRegion = region;
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

        string ResolveRewardGroupId(string internalProductId)
        {
            var product = TB_PRODUCT.Get(internalProductId);
            return product != null ? product.RewardGroupId ?? string.Empty : string.Empty;
        }

        string ResolveStoreProductId(string internalProductId)
        {
            var product = TB_PRODUCT.Get(internalProductId);
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

        public IReadOnlyList<PurchaseStorage.RefundSupportLogEntry> GetRefundSupportLogs()
        {
            var purchaseStorage = getPurchaseStorageOrNull();
            return purchaseStorage != null
                ? purchaseStorage.GetRefundSupportLogs()
                : Array.Empty<PurchaseStorage.RefundSupportLogEntry>();
        }

        public bool TryGetRefundSupportLog(string purchaseId, out PurchaseStorage.RefundSupportLogEntry entry)
        {
            var purchaseStorage = getPurchaseStorageOrNull();
            if (purchaseStorage == null)
            {
                entry = null;
                return false;
            }

            return purchaseStorage.TryGetRefundSupportLog(purchaseId, out entry);
        }

        public bool DeleteRefundSupportLog(string purchaseId)
        {
            var purchaseStorage = getPurchaseStorageOrNull();
            return purchaseStorage != null && purchaseStorage.RemoveRefundSupportLog(purchaseId);
        }

        public void ClearRefundSupportLogs()
        {
            getPurchaseStorageOrNull()?.ClearRefundSupportLogs();
        }

        public bool HasCachedSeasonPass(string internalProductId)
        {
            var purchaseStorage = getPurchaseStorageOrNull();
            return purchaseStorage != null && purchaseStorage.IsSeasonPassOwned(internalProductId);
        }

#if UNITY_PURCHASING
        StoreController _controller;
        bool _connected;
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
                CommonErrorType.PURCHASE_UNSUPPORTED_PLATFORM,
                "PurchaseManager is not supported in Editor."));
#else
            if (_initializeTask != null)
                return _initializeTask;

            _initializeTask = initializeIapAsync(ct);
            return _initializeTask;
#endif
        }

        /// <summary>
        /// 상품 구매를 수행한다. TB_PRODUCT에서 Kind를 조회하여 자동으로 구매 유형을 결정한다.
        /// </summary>
        public Task<CommonResult<PurchaseFinalResult>> PurchaseAsync(
            string internalProductId, CancellationToken ct = default)
        {
            var product = TB_PRODUCT.Get(internalProductId);
            if (product == null)
                return Task.FromResult(CommonResult<PurchaseFinalResult>.Failure(
                    CommonErrorType.PURCHASE_PRODUCT_NOT_FOUND,
                    $"Product not found: {internalProductId}"));

            var kind = ProductKindToPurchaseKind(product.Kind);
            return purchaseAndVerifyAsync(internalProductId, kind, ct);
        }

        public async Task<CommonResult<RetryInterruptedPurchaseResult>> RetryInterruptedPurchaseAsync(CancellationToken ct = default)
        {
            var purchaseStorage = getPurchaseStorageOrNull();
            if (purchaseStorage == null)
                return CommonResult<RetryInterruptedPurchaseResult>.Failure(
                    CommonErrorType.PURCHASE_INTERRUPTED_STORAGE_UNAVAILABLE,
                    "PurchaseStorage is not available.");

            var current = purchaseStorage.Current;
            if (!current.IsPurchaseInProgress)
                return CommonResult<RetryInterruptedPurchaseResult>.Success(
                    new RetryInterruptedPurchaseResult(
                        RetryInterruptedPurchaseStatus.SkippedNoCurrent,
                        string.Empty,
                        null,
                        string.Empty,
                        string.Empty,
                        Array.Empty<RewardData>()));

            if (string.IsNullOrEmpty(current.InternalProductId))
                return CommonResult<RetryInterruptedPurchaseResult>.Failure(
                    CommonErrorType.PURCHASE_INTERRUPTED_SNAPSHOT_PRODUCT_ID_MISSING,
                    "Interrupted purchase snapshot is missing internalProductId.");

            if (!TryResolveInterruptedPurchaseKind(current.InternalProductId, current.Kind, out var currentKind))
            {
                return CommonResult<RetryInterruptedPurchaseResult>.Failure(
                    CommonErrorType.PURCHASE_INTERRUPTED_SNAPSHOT_KIND_INVALID,
                    $"Interrupted purchase snapshot has invalid purchase kind: {current.Kind}");
            }

            if (current.StoreConfirmedLocal && !string.IsNullOrEmpty(current.PurchaseId))
            {
                var resumed = await resumeAfterStoreConfirmAsync(current.InternalProductId, currentKind, ct);
                if (resumed.IsFailure)
                    return CommonResult<RetryInterruptedPurchaseResult>.Failure(resumed.Error!);

                var finalAfterConfirm = resumed.Value!;
                return CommonResult<RetryInterruptedPurchaseResult>.Success(
                    new RetryInterruptedPurchaseResult(
                        RetryInterruptedPurchaseStatus.Retried,
                        finalAfterConfirm.InternalProductId,
                        finalAfterConfirm.Kind,
                        finalAfterConfirm.ResultStatus,
                        finalAfterConfirm.RewardGroupId,
                        finalAfterConfirm.AppliedRewards));
            }

            var resume = await purchaseAndVerifyAsync(current.InternalProductId, currentKind, ct, isRecoveryCall: true);
            if (resume.IsFailure)
                return CommonResult<RetryInterruptedPurchaseResult>.Failure(resume.Error!);

            var finalResult = resume.Value!;
            return CommonResult<RetryInterruptedPurchaseResult>.Success(
                new RetryInterruptedPurchaseResult(
                    RetryInterruptedPurchaseStatus.Retried,
                    finalResult.InternalProductId,
                    finalResult.Kind,
                    finalResult.ResultStatus,
                    finalResult.RewardGroupId,
                    finalResult.AppliedRewards));
        }

        // Store restore (manual/fallback). This is not the same as domain restore for SeasonPass/Rental.
        // Product-type restore should use server projection sync (future entitlements/restore snapshot path).
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
                    tcs.TrySetResult(CommonResult<bool>.Failure(CommonErrorType.PURCHASE_RESTORE_FAILED, error ?? "RestoreTransactions failed."));
            });

            if (ct.CanBeCanceled)
                ct.Register(() => tcs.TrySetCanceled(ct));

            var restore = await tcs.Task;
            if (restore.IsFailure)
                return CommonResult<EntitlementsSnapshot>.Failure(restore.Error!);

            return await SyncEntitlementsAsync(ct);
        }

        // SyncEntitlementsAsync updates PurchaseStorage local/cloud cache for purchase-domain restore state
        // (SeasonPass ownership only). noAds is game logic state and must not be sourced from server entitlements.
        // Rental remaining time is queried from server on demand via GetRentalRemainingMsAsync().
        // noAds 판단은 게임 로직이 남은 시간(ms) 기반으로 처리한다.
        public async Task<CommonResult<EntitlementsSnapshot>> SyncEntitlementsAsync(CancellationToken ct = default)
        {
            var result = await callFunctionAsync("getEntitlements", null, ct);
            if (result.IsFailure)
                return CommonResult<EntitlementsSnapshot>.Failure(result.Error!);

            var snapshot = ParseEntitlementsSnapshot(result.Value!);
            cacheEntitlementsSnapshot(snapshot);
            return CommonResult<EntitlementsSnapshot>.Success(snapshot);
        }

        public async Task<CommonResult<long>> GetRentalRemainingMsAsync(string internalProductId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(internalProductId))
                return CommonResult<long>.Failure(CommonErrorType.COMMON_INVALID_ARGUMENT, "internalProductId is required.");

            var sync = await SyncEntitlementsAsync(ct);
            if (sync.IsFailure)
                return CommonResult<long>.Failure(sync.Error!);

            var snapshot = sync.Value!;
            if (!snapshot.Rentals.TryGetValue(internalProductId, out var expiresAtUtcMs) || expiresAtUtcMs <= 0)
            {
                getPurchaseStorageOrNull()?.SetNoAdsRemainingMs(0L);
                return CommonResult<long>.Success(0L);
            }

            var serverNowUtcMs = snapshot.ServerNowUtcMs > 0
                ? snapshot.ServerNowUtcMs
                : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var remainingMs = expiresAtUtcMs - serverNowUtcMs;
            var clampedRemainingMs = remainingMs > 0 ? remainingMs : 0L;
            getPurchaseStorageOrNull()?.SetNoAdsRemainingMs(clampedRemainingMs);
            return CommonResult<long>.Success(clampedRemainingMs);
        }

        public async Task<CommonResult<RecentPurchaseItem>> GetLatestConsumablePurchase30dAsync(CancellationToken ct = default)
        {
#if UNITY_EDITOR
            return CommonResult<RecentPurchaseItem>.Failure(
                CommonErrorType.PURCHASE_UNSUPPORTED_PLATFORM,
                "PurchaseManager is not supported in Editor.");
#endif
            if (!_iapInitialized)
                return CommonResult<RecentPurchaseItem>.Failure(
                    CommonErrorType.PURCHASE_INIT_REQUIRED,
                    "PurchaseManager not initialized. Call InitializeAsync() first.");

            var data = new Dictionary<string, object> { ["pageSize"] = 1, ["kind"] = "Consumable" };
            var result = await callFunctionAsync("getRecentPurchases30d", data, ct);
            if (result.IsFailure)
                return CommonResult<RecentPurchaseItem>.Failure(result.Error!);

            var item = parseFirstRecentPurchaseItem(result.Value!);
            if (item == null)
                return CommonResult<RecentPurchaseItem>.Failure(
                    CommonErrorType.PURCHASE_RECENT_NOT_FOUND,
                    "No recent consumable purchase within 30 days.");

            return CommonResult<RecentPurchaseItem>.Success(item);
        }

        public async Task<CommonResult<RentalPurchaseItem>> GetLatestRentalPurchase30dAsync(CancellationToken ct = default)
        {
#if UNITY_EDITOR
            return CommonResult<RentalPurchaseItem>.Failure(
                CommonErrorType.PURCHASE_UNSUPPORTED_PLATFORM,
                "PurchaseManager is not supported in Editor.");
#endif
            if (!_iapInitialized)
                return CommonResult<RentalPurchaseItem>.Failure(
                    CommonErrorType.PURCHASE_INIT_REQUIRED,
                    "PurchaseManager not initialized. Call InitializeAsync() first.");

            var data = new Dictionary<string, object> { ["pageSize"] = 1, ["kind"] = "Rental" };
            var result = await callFunctionAsync("getRecentPurchases30d", data, ct);
            if (result.IsFailure)
                return CommonResult<RentalPurchaseItem>.Failure(result.Error!);

            var item = parseFirstRentalPurchaseItem(result.Value!);
            if (item == null)
                return CommonResult<RentalPurchaseItem>.Failure(
                    CommonErrorType.PURCHASE_RENTAL_LATEST_NOT_FOUND,
                    "No recent rental purchase within 30 days.");

            return CommonResult<RentalPurchaseItem>.Success(item);
        }

        // ── Firebase Callable Helper ─────────────────────────────

        async Task<CommonResult<Dictionary<string, object>>> callFunctionAsync(
            string functionName, Dictionary<string, object> data, CancellationToken ct)
        {
            try
            {
                var functions = string.IsNullOrEmpty(_functionsRegion)
                    ? FirebaseFunctions.DefaultInstance
                    : FirebaseFunctions.GetInstance(_functionsRegion);
                var callable = functions.GetHttpsCallable(functionName);
                var result = data != null
                    ? await callable.CallAsync(data)
                    : await callable.CallAsync();

                ct.ThrowIfCancellationRequested();

                var response = normalizeCallableResponse(result.Data);
                if (response == null)
                    return CommonResult<Dictionary<string, object>>.Failure(
                        CommonErrorType.PURCHASE_FUNCTION_RESPONSE_INVALID,
                        $"{functionName} returned unsupported response type: {(result.Data == null ? "null" : result.Data.GetType().FullName)}");

                return CommonResult<Dictionary<string, object>>.Success(response);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                var mapped = mapFirebaseException(functionName, ex);
                if (mapped.HasValue)
                {
                    var mappedError = mapped.Value.Error;
                    var firebaseCode = ex is FunctionsException fex ? fex.ErrorCode.ToString() : "N/A";
                    if (mappedError != null)
                    {
                        Debug.LogWarning(
                            $"[{Tag}] {functionName} mapped firebase error: {mappedError.Code} " +
                            $"(firebase={firebaseCode}) {mappedError.Message}");
                    }
                    return mapped.Value;
                }

                Debug.LogError($"[{Tag}] {functionName} failed: {ex.Message}");
                return CommonResult<Dictionary<string, object>>.Failure(
                    mapUnhandledFunctionErrorType(functionName), ex.Message);
            }
        }

        static CommonResult<Dictionary<string, object>>? mapFirebaseException(string functionName, Exception ex)
        {
            if (!(ex is FunctionsException fex)) return null;

            var isVerifyPurchase = functionName == "verifyPurchase";
            var isAckPurchaseClientGrant = functionName == "ackPurchaseClientGrant";
            var isGetRecentPurchases = functionName == "getRecentPurchases30d";
            var isGetEntitlements = functionName == "getEntitlements";

            switch (fex.ErrorCode)
            {
                case FunctionsErrorCode.Unauthenticated:
                    return CommonResult<Dictionary<string, object>>.Failure(
                        CommonErrorType.PURCHASE_UNAUTHENTICATED,
                        "Authentication required.");

                case FunctionsErrorCode.InvalidArgument:
                    if (isVerifyPurchase || isAckPurchaseClientGrant)
                    {
                        return CommonResult<Dictionary<string, object>>.Failure(
                            CommonErrorType.PURCHASE_VERIFY_INVALID_ARGUMENT,
                            $"Invalid {functionName} arguments.");
                    }
                    if (isGetRecentPurchases)
                    {
                        return CommonResult<Dictionary<string, object>>.Failure(
                            CommonErrorType.PURCHASE_RECENT_CALL_FAILED,
                            "Invalid getRecentPurchases request arguments.");
                    }
                    return CommonResult<Dictionary<string, object>>.Failure(
                        mapUnhandledFunctionErrorType(functionName), fex.Message);

                case FunctionsErrorCode.FailedPrecondition:
                    if (isVerifyPurchase || isAckPurchaseClientGrant)
                    {
                        return CommonResult<Dictionary<string, object>>.Failure(
                            CommonErrorType.PURCHASE_VERIFY_FAILED_PRECONDITION,
                            $"{functionName} failed precondition.");
                    }
                    return CommonResult<Dictionary<string, object>>.Failure(
                        mapUnhandledFunctionErrorType(functionName), fex.Message);

                case FunctionsErrorCode.PermissionDenied:
                    return CommonResult<Dictionary<string, object>>.Failure(
                        CommonErrorType.COMMON_AUTH, "Permission denied.");

                case FunctionsErrorCode.Unavailable:
                case FunctionsErrorCode.DeadlineExceeded:
                    return CommonResult<Dictionary<string, object>>.Failure(
                        mapNetworkFunctionErrorType(functionName),
                        "Network unavailable.");

                default:
                    return CommonResult<Dictionary<string, object>>.Failure(
                        mapUnhandledFunctionErrorType(functionName),
                        fex.Message);
            }
        }

        static Dictionary<string, object> normalizeCallableResponse(object data)
        {
            if (data == null)
                return null;

            if (data is IDictionary<string, object> stringObjectMap)
                return normalizeStringObjectMap(stringObjectMap);

            if (data is IDictionary anyMap)
                return normalizeAnyMap(anyMap);

            return null;
        }

        static Dictionary<string, object> normalizeStringObjectMap(IDictionary<string, object> source)
        {
            var result = new Dictionary<string, object>(source.Count);
            foreach (var kv in source)
                result[kv.Key] = normalizeCallableValue(kv.Value);
            return result;
        }

        static Dictionary<string, object> normalizeAnyMap(IDictionary source)
        {
            var result = new Dictionary<string, object>(source.Count);
            foreach (DictionaryEntry entry in source)
            {
                if (entry.Key == null) continue;
                result[entry.Key.ToString()] = normalizeCallableValue(entry.Value);
            }
            return result;
        }

        static object normalizeCallableValue(object value)
        {
            if (value == null)
                return null;

            if (value is IDictionary<string, object> stringObjectMap)
                return normalizeStringObjectMap(stringObjectMap);

            if (value is IDictionary anyMap)
                return normalizeAnyMap(anyMap);

            if (value is IList list && !(value is string))
            {
                var normalized = new List<object>(list.Count);
                foreach (var item in list)
                    normalized.Add(normalizeCallableValue(item));
                return normalized;
            }

            return value;
        }

        static CommonErrorType mapUnhandledFunctionErrorType(string functionName)
        {
            switch (functionName)
            {
                case "getRecentPurchases30d":
                    return CommonErrorType.PURCHASE_RECENT_CALL_FAILED;
                case "getEntitlements":
                    return CommonErrorType.PURCHASE_ENTITLEMENTS_CALL_FAILED;
                case "verifyPurchase":
                case "ackPurchaseClientGrant":
                    return CommonErrorType.PURCHASE_VERIFY_CALL_FAILED;
                case "ackPurchaseStoreConfirm":
                    return CommonErrorType.PURCHASE_STORE_CONFIRM_ACK_CALL_FAILED;
                default:
                    return CommonErrorType.COMMON_SERVER;
            }
        }

        static CommonErrorType mapNetworkFunctionErrorType(string functionName)
        {
            switch (functionName)
            {
                case "getRecentPurchases30d":
                    return CommonErrorType.PURCHASE_RECENT_CALL_FAILED;
                case "getEntitlements":
                    return CommonErrorType.PURCHASE_ENTITLEMENTS_CALL_FAILED;
                case "ackPurchaseStoreConfirm":
                    return CommonErrorType.PURCHASE_STORE_CONFIRM_ACK_CALL_FAILED;
                case "ackPurchaseClientGrant":
                default:
                    return CommonErrorType.PURCHASE_NETWORK_UNAVAILABLE;
            }
        }

        // ── Core Flow ──────────────────────────────────────────────

        async Task<CommonResult> initializeIapAsync(CancellationToken ct)
        {
            if (_productCatalog == null)
                return CommonResult.Failure(CommonErrorType.PURCHASE_INIT_FAILED, "ProductCatalog not set. Call SetProductCatalog().");

            try
            {
                _controller = UnityIAPServices.StoreController();

                _controller.OnPurchasePending += onPurchasePending;
                _controller.OnPurchaseFailed += onPurchaseFailed;
                _controller.OnStoreDisconnected += onStoreDisconnected;

                await _controller.Connect();
                _connected = true;
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
                    return CommonResult.Failure(CommonErrorType.PURCHASE_PRODUCT_FETCH_FAILED, ex.Message);

                return CommonResult.Failure(CommonErrorType.PURCHASE_INIT_FAILED, ex.Message);
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
                return CommonResult<PurchaseFinalResult>.Failure(CommonErrorType.PURCHASE_UNAUTHENTICATED,
                    "Authentication required before purchase. Sign in with Guest, Google, or Apple first.");

            if (_purchaseStore == null)
                return CommonResult<PurchaseFinalResult>.Failure(CommonErrorType.PURCHASE_STORE_NOT_SET, "PurchaseStore not set. Call SetPurchaseStore().");

            if (_purchaseInProgress)
                return CommonResult<PurchaseFinalResult>.Failure(CommonErrorType.PURCHASE_PURCHASE_IN_PROGRESS, "Another purchase is already in progress.");

            var purchaseStorage = getPurchaseStorageOrNull();
            if (!isRecoveryCall && (purchaseStorage?.IsPurchaseInProgress ?? false))
            {
                return CommonResult<PurchaseFinalResult>.Failure(
                    CommonErrorType.PURCHASE_PURCHASE_IN_PROGRESS,
                    "Interrupted purchase exists. Call RetryInterruptedPurchaseAsync() before starting a new purchase.");
            }

            var clearCurrentOnExit = true;
            var purchaseKindString = PurchaseKindToString(kind);
            var recoverClientGrantApplied =
                (purchaseStorage?.IsPurchaseInProgress ?? false) &&
                string.Equals(purchaseStorage.CurrentInternalProductId, internalProductId, StringComparison.Ordinal) &&
                string.Equals(purchaseStorage.CurrentKind, purchaseKindString, StringComparison.Ordinal) &&
                string.Equals(purchaseStorage.CurrentStoreKey, _purchaseStore.StoreKey, StringComparison.Ordinal) &&
                purchaseStorage.CurrentClientGrantApplied;
            var recoverClientGrantReported =
                (purchaseStorage?.IsPurchaseInProgress ?? false) &&
                string.Equals(purchaseStorage.CurrentInternalProductId, internalProductId, StringComparison.Ordinal) &&
                string.Equals(purchaseStorage.CurrentKind, purchaseKindString, StringComparison.Ordinal) &&
                string.Equals(purchaseStorage.CurrentStoreKey, _purchaseStore.StoreKey, StringComparison.Ordinal) &&
                purchaseStorage.CurrentClientGrantReported;
            var recoverStoreConfirmedLocal =
                (purchaseStorage?.IsPurchaseInProgress ?? false) &&
                string.Equals(purchaseStorage.CurrentInternalProductId, internalProductId, StringComparison.Ordinal) &&
                string.Equals(purchaseStorage.CurrentKind, purchaseKindString, StringComparison.Ordinal) &&
                string.Equals(purchaseStorage.CurrentStoreKey, _purchaseStore.StoreKey, StringComparison.Ordinal) &&
                purchaseStorage.CurrentStoreConfirmedLocal;
            _purchaseInProgress = true;
            try
            {
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

                        // 매칭 안 되는 stale pending orders를 ConfirmPurchase로 소비 처리
                        // (서버 검증 실패로 stuck된 취소/만료 구매를 정리하여 재구매를 가능하게 함)
                        if (preFetch.Value?.PendingOrders != null && preFetch.Value.PendingOrders.Count > 0)
                        {
                            Debug.Log($"[{Tag}] Pre-fetch: consuming {preFetch.Value.PendingOrders.Count} stale pending order(s) to unblock store.");
                            foreach (var stale in preFetch.Value.PendingOrders)
                            {
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
                    _controller.PurchaseProduct(internalProductId);

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

                        return CommonResult<PurchaseFinalResult>.Failure(CommonErrorType.PURCHASE_PURCHASE_REQUEST_FAILED, ex.Message);
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
                                CommonErrorType.PURCHASE_STORE_FAILED,
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
                    var appliedRewards = Array.Empty<RewardData>();
                    var shouldApplyLocalReward = status == "GRANTED";
                    if (!shouldApplyLocalReward && status == "ALREADY_GRANTED" &&
                        (clientGrantStatus == "PENDING" || clientGrantStatus == "FAILED_REPORTED"))
                    {
                        shouldApplyLocalReward = !(purchaseStorage?.CurrentClientGrantApplied ?? false);
                    }

                    if (shouldApplyLocalReward)
                    {
                        if (!string.IsNullOrEmpty(rewardGroupId))
                        {
                            CommonResult<RewardManager.RewardApplyResult> localGrant;
                            try
                            {
                                localGrant = Singleton.Get<RewardManager>().ApplyRewardGroup(rewardGroupId);
                            }
                            catch (Exception ex)
                            {
                                if (!string.IsNullOrEmpty(response.PurchaseId))
                                {
                                    var report = await reportPurchaseClientGrantResultAsync(response.PurchaseId, "FAILED_REPORTED", finalizeCt);
                                    if (report.IsSuccess)
                                    {
                                        clientGrantStatus = "FAILED_REPORTED";
                                        purchaseStorage?.UpsertRefundSupportLog(
                                            response.PurchaseId,
                                            internalProductId,
                                            purchaseKindString,
                                            _purchaseStore.StoreKey,
                                            response.VerifyStatus,
                                            clientGrantStatus,
                                            storeConfirmStatus);
                                        clearCurrentOnExit = false;
                                    }
                                    else
                                    {
                                        clearCurrentOnExit = false;
                                    }
                                }
                                else
                                {
                                    clearCurrentOnExit = false;
                                }
                                return CommonResult<PurchaseFinalResult>.Failure(
                                    CommonErrorType.PURCHASE_STORE_FAILED,
                                    $"Local reward apply threw exception: {ex.Message}");
                            }

                            if (localGrant.IsFailure)
                            {
                                if (!string.IsNullOrEmpty(response.PurchaseId))
                                {
                                    var report = await reportPurchaseClientGrantResultAsync(response.PurchaseId, "FAILED_REPORTED", finalizeCt);
                                    if (report.IsSuccess)
                                    {
                                        clientGrantStatus = "FAILED_REPORTED";
                                        purchaseStorage?.UpsertRefundSupportLog(
                                            response.PurchaseId,
                                            internalProductId,
                                            purchaseKindString,
                                            _purchaseStore.StoreKey,
                                            response.VerifyStatus,
                                            clientGrantStatus,
                                            storeConfirmStatus);
                                        clearCurrentOnExit = false;
                                    }
                                    else
                                    {
                                        clearCurrentOnExit = false;
                                    }
                                }
                                else
                                {
                                    clearCurrentOnExit = false;
                                }
                                return CommonResult<PurchaseFinalResult>.Failure(localGrant.Error!);
                            }

                            var rewardApply = localGrant.Value!;
                            rewardGroupId = rewardApply.RewardGroupId;
                            appliedRewards = rewardApply.AppliedRewards ?? Array.Empty<RewardData>();
                        }

                        purchaseStorage?.MarkClientGrantApplied();
                    }

                    var needClientGrantReport =
                        (purchaseStorage?.CurrentClientGrantApplied ?? false) &&
                        !(purchaseStorage?.CurrentClientGrantReported ?? false) &&
                        clientGrantStatus != "APPLIED_ACKED";

                    if (needClientGrantReport && !string.IsNullOrEmpty(response.PurchaseId) && !(purchaseStorage?.CurrentClientGrantReported ?? false))
                    {
                        var report = await reportPurchaseClientGrantResultAsync(response.PurchaseId, "APPLIED_ACKED", finalizeCt);
                        if (report.IsFailure)
                        {
                            clearCurrentOnExit = false;
                            return CommonResult<PurchaseFinalResult>.Failure(report.Error!);
                        }
                        purchaseStorage?.MarkClientGrantReported();
                        clientGrantStatus = "APPLIED_ACKED";
                        purchaseStorage?.UpsertRefundSupportLog(
                            response.PurchaseId,
                            internalProductId,
                            purchaseKindString,
                            _purchaseStore.StoreKey,
                            response.VerifyStatus,
                            clientGrantStatus,
                            storeConfirmStatus);
                    }

                    purchaseStorage?.ClearCurrent();
                    clearCurrentOnExit = false;
                    return CommonResult<PurchaseFinalResult>.Success(
                        new PurchaseFinalResult(internalProductId, kind, status, rewardGroupId, appliedRewards));
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
                        CommonErrorType.PURCHASE_VERIFY_REJECTED_UNKNOWN,
                        $"{status}:{rejectReason} (cancelled purchase consumed, retry possible)");
                }

                var keepCurrentForRecovery =
                    status == "PENDING" ||
                    (status == "REJECTED" && (
                        rejectReason == "STORE_VERIFY_ERROR" ||
                        rejectReason == "STORE_VERIFY_MISSING_PURCHASE_TIME" ||
                        (!string.IsNullOrEmpty(rejectReason) && rejectReason.StartsWith("STORE_VERIFY_PARSE_ERROR", StringComparison.Ordinal))
                    ));
                if (keepCurrentForRecovery)
                    clearCurrentOnExit = false;

                CommonErrorType errorType;
                if (rejectReason == "SEASON_PASS_ALREADY_OWNED")
                    errorType = CommonErrorType.PURCHASE_SEASON_PASS_ALREADY_OWNED;
                else
                    errorType = CommonErrorType.PURCHASE_VERIFY_REJECTED_UNKNOWN;

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
                    CommonErrorType.PURCHASE_INTERRUPTED_STORAGE_UNAVAILABLE,
                    "PurchaseStorage is not available.");

            var current = purchaseStorage.Current;
            if (!current.IsPurchaseInProgress || !current.StoreConfirmedLocal || string.IsNullOrEmpty(current.PurchaseId))
                return CommonResult<PurchaseFinalResult>.Failure(
                    CommonErrorType.PURCHASE_INTERRUPTED_SNAPSHOT_KIND_INVALID,
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
            var appliedRewards = Array.Empty<RewardData>();
            var shouldApplyLocalReward =
                !purchaseStorage.CurrentClientGrantApplied &&
                (clientGrantStatus == "PENDING" || clientGrantStatus == "FAILED_REPORTED");

            if (shouldApplyLocalReward)
            {
                if (!string.IsNullOrEmpty(rewardGroupId))
                {
                    CommonResult<RewardManager.RewardApplyResult> localGrant;
                    try
                    {
                        localGrant = Singleton.Get<RewardManager>().ApplyRewardGroup(rewardGroupId);
                    }
                    catch (Exception ex)
                    {
                        if (!string.IsNullOrEmpty(current.PurchaseId))
                        {
                            var report = await reportPurchaseClientGrantResultAsync(current.PurchaseId, "FAILED_REPORTED", ct);
                            if (report.IsSuccess)
                            {
                                clientGrantStatus = "FAILED_REPORTED";
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

                        return CommonResult<PurchaseFinalResult>.Failure(
                            CommonErrorType.PURCHASE_STORE_FAILED,
                            $"Local reward apply threw exception: {ex.Message}");
                    }

                    if (localGrant.IsFailure)
                    {
                        if (!string.IsNullOrEmpty(current.PurchaseId))
                        {
                            var report = await reportPurchaseClientGrantResultAsync(current.PurchaseId, "FAILED_REPORTED", ct);
                            if (report.IsSuccess)
                            {
                                clientGrantStatus = "FAILED_REPORTED";
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

                        return CommonResult<PurchaseFinalResult>.Failure(localGrant.Error!);
                    }

                    var rewardApply = localGrant.Value!;
                    rewardGroupId = rewardApply.RewardGroupId;
                    appliedRewards = rewardApply.AppliedRewards ?? Array.Empty<RewardData>();
                }

                purchaseStorage.MarkClientGrantApplied();
            }

            var needClientGrantReport =
                purchaseStorage.CurrentClientGrantApplied &&
                !purchaseStorage.CurrentClientGrantReported &&
                clientGrantStatus != "APPLIED_ACKED";

            if (needClientGrantReport)
            {
                var report = await reportPurchaseClientGrantResultAsync(current.PurchaseId, "APPLIED_ACKED", ct);
                if (report.IsFailure)
                    return CommonResult<PurchaseFinalResult>.Failure(report.Error!);

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

            purchaseStorage.ClearCurrent();
            return CommonResult<PurchaseFinalResult>.Success(
                new PurchaseFinalResult(internalProductId, kind, "ALREADY_GRANTED", rewardGroupId, appliedRewards));
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

            var result = await callFunctionAsync("verifyPurchase", data, ct);
            if (result.IsFailure)
                return CommonResult<VerifyPurchaseResponse>.Failure(result.Error!);

            var response = result.Value!;
            var resultStatus = response.TryGetValue("resultStatus", out var rs) ? rs as string ?? "" : "";
            var rejectReason = response.TryGetValue("rejectReason", out var rr) ? rr as string ?? "" : "";
            var purchaseId = response.TryGetValue("purchaseId", out var pid) ? pid as string ?? "" : "";
            var verifyStatus = response.TryGetValue("verifyStatus", out var vs) ? vs as string ?? "" : resultStatus;
            var clientGrantStatus = response.TryGetValue("clientGrantStatus", out var cgs) ? cgs as string ?? "" : "";
            var storeConfirmStatus = response.TryGetValue("storeConfirmStatus", out var scs) ? scs as string ?? "" : "";

            EntitlementsSnapshot? snapshot = null;
            if (response.TryGetValue("entitlementsSnapshot", out var snapObj) && snapObj is Dictionary<string, object> snap)
            {
                snapshot = ParseEntitlementsSnapshot(snap);
                cacheEntitlementsSnapshot(snapshot.Value);
            }

            return CommonResult<VerifyPurchaseResponse>.Success(
                new VerifyPurchaseResponse(resultStatus, rejectReason, purchaseId, verifyStatus, clientGrantStatus, storeConfirmStatus, snapshot));
        }

        async Task<CommonResult> reportPurchaseClientGrantResultAsync(string purchaseId, string clientGrantStatus, CancellationToken ct)
        {
            var data = new Dictionary<string, object>
            {
                ["purchaseId"] = purchaseId,
                ["clientGrantStatus"] = clientGrantStatus,
            };

            var result = await callFunctionAsync("ackPurchaseClientGrant", data, ct);
            if (result.IsFailure)
                return CommonResult.Failure(result.Error!);

            return CommonResult.Ok();
        }

        async Task<CommonResult> ackPurchaseStoreConfirmAsync(string purchaseId, CancellationToken ct)
        {
            var data = new Dictionary<string, object>
            {
                ["purchaseId"] = purchaseId,
            };

            var result = await callFunctionAsync("ackPurchaseStoreConfirm", data, ct);
            if (result.IsFailure)
                return CommonResult.Failure(result.Error!);

            return CommonResult.Ok();
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
                    CommonErrorType.PURCHASE_PURCHASE_REQUEST_FAILED,
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
            _connected = false;
            Debug.LogWarning($"[{Tag}] Store disconnected: {desc}");
        }

        protected override void OnDestroy()
        {
            if (_controller != null)
            {
                _controller.OnPurchasePending -= onPurchasePending;
                _controller.OnPurchaseFailed -= onPurchaseFailed;
                _controller.OnStoreDisconnected -= onStoreDisconnected;
            }
            base.OnDestroy();
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
            Task.FromResult(CommonResult.Failure(CommonErrorType.IAP_NOT_SUPPORTED, "Unity Purchasing not available."));

        static readonly Task<CommonResult<PurchaseFinalResult>> _notSupported =
            Task.FromResult(CommonResult<PurchaseFinalResult>.Failure(CommonErrorType.IAP_NOT_SUPPORTED, "Unity Purchasing not available."));

        static readonly Task<CommonResult<EntitlementsSnapshot>> _notSupportedSnapshot =
            Task.FromResult(CommonResult<EntitlementsSnapshot>.Failure(CommonErrorType.IAP_NOT_SUPPORTED, "Unity Purchasing not available."));

        static readonly Task<CommonResult<RecentPurchaseItem>> _notSupportedRecent =
            Task.FromResult(CommonResult<RecentPurchaseItem>.Failure(CommonErrorType.IAP_NOT_SUPPORTED, "Unity Purchasing not available."));

        static readonly Task<CommonResult<RentalPurchaseItem>> _notSupportedRental =
            Task.FromResult(CommonResult<RentalPurchaseItem>.Failure(CommonErrorType.IAP_NOT_SUPPORTED, "Unity Purchasing not available."));
        static readonly Task<CommonResult<long>> _notSupportedLong =
            Task.FromResult(CommonResult<long>.Failure(CommonErrorType.IAP_NOT_SUPPORTED, "Unity Purchasing not available."));

        public Task<CommonResult> InitializeAsync(CancellationToken ct = default) => _notSupportedInit;
        public Task<CommonResult<PurchaseFinalResult>> PurchaseAsync(string internalProductId, CancellationToken ct = default) => _notSupported;
        static readonly Task<CommonResult<RetryInterruptedPurchaseResult>> _notSupportedRetryInterrupted =
            Task.FromResult(CommonResult<RetryInterruptedPurchaseResult>.Failure(CommonErrorType.IAP_NOT_SUPPORTED, "Unity Purchasing not available."));

        public Task<CommonResult<RetryInterruptedPurchaseResult>> RetryInterruptedPurchaseAsync(CancellationToken ct = default)
            => _notSupportedRetryInterrupted;
        public Task<CommonResult<EntitlementsSnapshot>> RestoreAsync(CancellationToken ct = default) => _notSupportedSnapshot;
        public Task<CommonResult<EntitlementsSnapshot>> SyncEntitlementsAsync(CancellationToken ct = default) => _notSupportedSnapshot;
        public Task<CommonResult<long>> GetRentalRemainingMsAsync(string internalProductId, CancellationToken ct = default) => _notSupportedLong;
        public Task<CommonResult<RecentPurchaseItem>> GetLatestConsumablePurchase30dAsync(CancellationToken ct = default) => _notSupportedRecent;
        public Task<CommonResult<RentalPurchaseItem>> GetLatestRentalPurchase30dAsync(CancellationToken ct = default) => _notSupportedRental;
#endif

        // ── Helpers ───────────────────────────────────────────────

        static EntitlementsSnapshot ParseEntitlementsSnapshot(Dictionary<string, object> snap)
        {
            var seasonPasses = new List<string>();
            if (snap.TryGetValue("ownedSeasonPasses", out var sp) && sp is IEnumerable<object> spList)
            {
                foreach (var s in spList)
                {
                    if (s is string str)
                        seasonPasses.Add(str);
                }
            }

            var balances = new Dictionary<string, long>();
            if (snap.TryGetValue("currencyBalances", out var cb) && cb is Dictionary<string, object> cbMap)
            {
                foreach (var kv in cbMap)
                    balances[kv.Key] = Convert.ToInt64(kv.Value);
            }

            var rentals = new Dictionary<string, long>();
            if (snap.TryGetValue("rentals", out var rentalsObj) && rentalsObj is Dictionary<string, object> rentalsMap)
            {
                foreach (var kv in rentalsMap)
                    rentals[kv.Key] = Convert.ToInt64(kv.Value);
            }

            var serverNowUtcMs = getLong(snap, "serverNowUtcMs");

            return new EntitlementsSnapshot(seasonPasses, balances, rentals, serverNowUtcMs);
        }

        static void cacheEntitlementsSnapshot(EntitlementsSnapshot snapshot)
        {
            var purchaseStorage = getPurchaseStorageOrNull();
            if (purchaseStorage == null)
                return;

            purchaseStorage.ReplaceSeasonPassOwnership(snapshot.OwnedSeasonPasses);
        }

        static PurchaseKind ProductKindToPurchaseKind(ProductKind kind)
        {
            switch (kind)
            {
                case ProductKind.Consumable: return PurchaseKind.Consumable;
                case ProductKind.Rental: return PurchaseKind.Rental;
                case ProductKind.Subscription: return PurchaseKind.Subscription;
                case ProductKind.SeasonPass: return PurchaseKind.SeasonPass;
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

            var product = TB_PRODUCT.Get(internalProductId);
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
                var accountManager = AccountManager.Instance;
                if (accountManager == null)
                    return CommonResult<bool>.Success(false);

                return await accountManager.EnsurePurchaseLoginReadyAsync(ct);
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

        static RecentPurchaseItem parseFirstRecentPurchaseItem(Dictionary<string, object> root)
        {
            if (!root.TryGetValue("items", out var itemsObj)) return null;
            if (!(itemsObj is IList<object> items) || items.Count == 0) return null;
            if (!(items[0] is IDictionary<string, object> first)) return null;

            return new RecentPurchaseItem
            {
                purchaseId = getString(first, "purchaseId"),
                internalProductId = getString(first, "internalProductId"),
                storePurchasedAtMs = getLong(first, "storePurchasedAt"),
                status = getString(first, "status"),
            };
        }

        static RentalPurchaseItem parseFirstRentalPurchaseItem(Dictionary<string, object> root)
        {
            if (!root.TryGetValue("items", out var itemsObj)) return null;
            if (!(itemsObj is IList<object> items) || items.Count == 0) return null;
            if (!(items[0] is IDictionary<string, object> first)) return null;

            return new RentalPurchaseItem
            {
                purchaseId = getString(first, "purchaseId"),
                internalProductId = getString(first, "internalProductId"),
                storePurchasedAtMs = getLong(first, "storePurchasedAt"),
                status = getString(first, "status"),
            };
        }

        static string getString(IDictionary<string, object> m, string key)
            => (m.TryGetValue(key, out var v) && v != null) ? v.ToString() : "";

        static long getLong(IDictionary<string, object> m, string key)
        {
            if (!m.TryGetValue(key, out var v) || v == null) return 0;
            if (v is long l) return l;
            if (v is int i) return i;
            if (v is double d) return (long)d;
            if (long.TryParse(v.ToString(), out var parsed)) return parsed;
            return 0;
        }

        static PurchaseStorage getPurchaseStorageOrNull()
        {
            try
            {
                var gameStorageManager = GameStorageManager.Instance;
                return gameStorageManager != null ? gameStorageManager.Purchase : null;
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

        public readonly struct VerifyPurchaseResponse
        {
            public VerifyPurchaseResponse(
                string resultStatus,
                string rejectReason,
                string purchaseId,
                string verifyStatus,
                string clientGrantStatus,
                string storeConfirmStatus,
                EntitlementsSnapshot? snapshot)
            {
                ResultStatus = resultStatus;
                RejectReason = rejectReason;
                PurchaseId = purchaseId;
                VerifyStatus = verifyStatus;
                ClientGrantStatus = clientGrantStatus;
                StoreConfirmStatus = storeConfirmStatus;
                Snapshot = snapshot;
            }

            public string ResultStatus { get; }
            public string RejectReason { get; }
            public string PurchaseId { get; }
            public string VerifyStatus { get; }
            public string ClientGrantStatus { get; }
            public string StoreConfirmStatus { get; }
            public EntitlementsSnapshot? Snapshot { get; }
        }

        public readonly struct EntitlementsSnapshot
        {
            public EntitlementsSnapshot(
                IReadOnlyList<string> ownedSeasonPasses,
                IReadOnlyDictionary<string, long> currencyBalances,
                IReadOnlyDictionary<string, long> rentals,
                long serverNowUtcMs)
            {
                OwnedSeasonPasses = ownedSeasonPasses;
                CurrencyBalances = currencyBalances;
                Rentals = rentals;
                ServerNowUtcMs = serverNowUtcMs;
            }
            public IReadOnlyList<string> OwnedSeasonPasses { get; }
            public IReadOnlyDictionary<string, long> CurrencyBalances { get; }
            public IReadOnlyDictionary<string, long> Rentals { get; }
            public long ServerNowUtcMs { get; }
        }

        public readonly struct PurchaseFinalResult
        {
            public PurchaseFinalResult(
                string internalProductId,
                PurchaseKind kind,
                string resultStatus,
                string rewardGroupId,
                RewardData[] appliedRewards)
            {
                InternalProductId = internalProductId;
                Kind = kind;
                ResultStatus = resultStatus;
                RewardGroupId = rewardGroupId ?? string.Empty;
                AppliedRewards = appliedRewards ?? Array.Empty<RewardData>();
            }

            public string InternalProductId { get; }
            public PurchaseKind Kind { get; }
            public string ResultStatus { get; }
            public string RewardGroupId { get; }
            public RewardData[] AppliedRewards { get; }
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
            {
                Status = status;
                InternalProductId = internalProductId ?? string.Empty;
                Kind = kind;
                ResultStatus = resultStatus ?? string.Empty;
                RewardGroupId = rewardGroupId ?? string.Empty;
                AppliedRewards = appliedRewards ?? Array.Empty<RewardData>();
            }

            public RetryInterruptedPurchaseStatus Status { get; }
            public string InternalProductId { get; }
            public PurchaseKind? Kind { get; }
            public string ResultStatus { get; }
            public string RewardGroupId { get; }
            public RewardData[] AppliedRewards { get; }
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

        public sealed class RecentPurchaseItem
        {
            public string purchaseId;
            public string internalProductId;
            public long storePurchasedAtMs;
            public string status;
        }

        public sealed class RentalPurchaseItem
        {
            public string purchaseId;
            public string internalProductId;
            public long storePurchasedAtMs;
            public string status;
        }
    }
}
