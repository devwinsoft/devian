using System;
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
            return TB_PRODUCT.Get(internalProductId).RewardGroupId;
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

        public async Task<CommonResult<EntitlementsSnapshot>> RestoreAsync(CancellationToken ct = default)
        {
            if (!_iapInitialized)
                return CommonResult<EntitlementsSnapshot>.Failure(CommonErrorType.PURCHASE_INIT_REQUIRED,
                    "PurchaseManager not initialized. Call InitializeAsync() first.");

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

        public async Task<CommonResult<EntitlementsSnapshot>> SyncEntitlementsAsync(CancellationToken ct = default)
        {
            var result = await callFunctionAsync("getEntitlements", null, ct);
            if (result.IsFailure)
                return CommonResult<EntitlementsSnapshot>.Failure(result.Error!);

            return CommonResult<EntitlementsSnapshot>.Success(ParseEntitlementsSnapshot(result.Value!));
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

            var data = new Dictionary<string, object> { ["pageSize"] = 1 };
            var result = await callFunctionAsync("getRecentRentalPurchases30d", data, ct);
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

                var response = result.Data as Dictionary<string, object>;
                if (response == null)
                    return CommonResult<Dictionary<string, object>>.Failure(
                        CommonErrorType.PURCHASE_FUNCTION_RESPONSE_INVALID,
                        $"{functionName} returned null.");

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
            var isGetRecentPurchases = functionName == "getRecentPurchases30d";
            var isGetRecentRentalPurchases = functionName == "getRecentRentalPurchases30d";
            var isGetEntitlements = functionName == "getEntitlements";

            switch (fex.ErrorCode)
            {
                case FunctionsErrorCode.Unauthenticated:
                    return CommonResult<Dictionary<string, object>>.Failure(
                        CommonErrorType.PURCHASE_UNAUTHENTICATED,
                        "Authentication required.");

                case FunctionsErrorCode.InvalidArgument:
                    if (isVerifyPurchase)
                    {
                        return CommonResult<Dictionary<string, object>>.Failure(
                            CommonErrorType.PURCHASE_VERIFY_INVALID_ARGUMENT,
                            "Invalid verifyPurchase arguments.");
                    }
                    if (isGetRecentPurchases || isGetRecentRentalPurchases)
                    {
                        return CommonResult<Dictionary<string, object>>.Failure(
                            CommonErrorType.PURCHASE_RECENT_CALL_FAILED,
                            "Invalid getRecentPurchases request arguments.");
                    }
                    return CommonResult<Dictionary<string, object>>.Failure(
                        mapUnhandledFunctionErrorType(functionName), fex.Message);

                case FunctionsErrorCode.FailedPrecondition:
                    if (isVerifyPurchase)
                    {
                        return CommonResult<Dictionary<string, object>>.Failure(
                            CommonErrorType.PURCHASE_VERIFY_FAILED_PRECONDITION,
                            "verifyPurchase failed precondition.");
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

        static CommonErrorType mapUnhandledFunctionErrorType(string functionName)
        {
            switch (functionName)
            {
                case "getRecentPurchases30d":
                    return CommonErrorType.PURCHASE_RECENT_CALL_FAILED;
                case "getRecentRentalPurchases30d":
                    return CommonErrorType.PURCHASE_RENTAL_LATEST_CALL_FAILED;
                case "getEntitlements":
                    return CommonErrorType.PURCHASE_ENTITLEMENTS_CALL_FAILED;
                case "verifyPurchase":
                    return CommonErrorType.PURCHASE_VERIFY_CALL_FAILED;
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
                case "getRecentRentalPurchases30d":
                    return CommonErrorType.PURCHASE_RENTAL_LATEST_CALL_FAILED;
                case "getEntitlements":
                    return CommonErrorType.PURCHASE_ENTITLEMENTS_CALL_FAILED;
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
            string internalProductId, PurchaseKind kind, CancellationToken ct)
        {
            if (!_iapInitialized)
                return CommonResult<PurchaseFinalResult>.Failure(CommonErrorType.PURCHASE_INIT_REQUIRED,
                    "PurchaseManager not initialized. Call InitializeAsync() first.");

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

            _purchaseInProgress = true;
            try
            {
                PendingOrder pendingOrder;

                var storeProductId = ResolveStoreProductId(internalProductId);
                if (!TryTakeDeferredPendingOrder(storeProductId, out pendingOrder))
                {
                    _purchaseTcs = new TaskCompletionSource<PendingOrder>();
                    _controller.PurchaseProduct(internalProductId);

                    try
                    {
                        pendingOrder = await _purchaseTcs.Task;
                    }
                    catch (Exception ex)
                    {
                        return CommonResult<PurchaseFinalResult>.Failure(CommonErrorType.PURCHASE_PURCHASE_REQUEST_FAILED, ex.Message);
                    }
                    finally
                    {
                        _purchaseTcs = null;
                    }
                }
                else
                {
                    Debug.Log($"[{Tag}] Reusing deferred pending order for {storeProductId}.");
                }

                ct.ThrowIfCancellationRequested();

                var store = _purchaseStore.StoreKey;
                var payload = _purchaseStore.BuildVerifyPayload(pendingOrder.Info.Receipt);
                var verifyResult = await verifyPurchaseAsync(internalProductId, storeProductId, kind, store, payload, ct);
                if (verifyResult.IsFailure)
                    return CommonResult<PurchaseFinalResult>.Failure(verifyResult.Error!);

                var response = verifyResult.Value!;
                var status = response.ResultStatus;

                if (status == "GRANTED" || status == "ALREADY_GRANTED")
                {
                    _controller.ConfirmPurchase(pendingOrder);

                    if (status == "GRANTED")
                    {
                        var rewardGroupId = ResolveRewardGroupId(internalProductId);
                        Singleton.Get<RewardManager>().ApplyRewardGroupId(rewardGroupId);
                    }

                    return CommonResult<PurchaseFinalResult>.Success(
                        new PurchaseFinalResult(internalProductId, kind, status));
                }

                var rejectReason = response.RejectReason;
                CommonErrorType errorType;
                if (rejectReason == "RENTAL_ALREADY_ACTIVE")
                    errorType = CommonErrorType.PURCHASE_RENTAL_ALREADY_ACTIVE;
                else if (rejectReason == "SEASON_PASS_ALREADY_OWNED")
                    errorType = CommonErrorType.PURCHASE_SEASON_PASS_ALREADY_OWNED;
                else
                    errorType = CommonErrorType.PURCHASE_VERIFY_REJECTED_UNKNOWN;

                return CommonResult<PurchaseFinalResult>.Failure(
                    errorType, $"{status}:{rejectReason}");
            }
            finally
            {
                _purchaseInProgress = false;
            }
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
                ["kind"] = PurchaseKindToString(kind),
                ["payload"] = payload,
            };

            var result = await callFunctionAsync("verifyPurchase", data, ct);
            if (result.IsFailure)
                return CommonResult<VerifyPurchaseResponse>.Failure(result.Error!);

            var response = result.Value!;
            var resultStatus = response.TryGetValue("resultStatus", out var rs) ? rs as string ?? "" : "";
            var rejectReason = response.TryGetValue("rejectReason", out var rr) ? rr as string ?? "" : "";

            EntitlementsSnapshot? snapshot = null;
            if (response.TryGetValue("entitlementsSnapshot", out var snapObj) && snapObj is Dictionary<string, object> snap)
                snapshot = ParseEntitlementsSnapshot(snap);

            return CommonResult<VerifyPurchaseResponse>.Success(
                new VerifyPurchaseResponse(resultStatus, rejectReason, snapshot));
        }

        // ── Event Handlers ────────────────────────────────────────

        void onPurchasePending(PendingOrder order)
        {
            if (_purchaseTcs != null)
            {
                _purchaseTcs.TrySetResult(order);
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

        public Task<CommonResult> InitializeAsync(CancellationToken ct = default) => _notSupportedInit;
        public Task<CommonResult<PurchaseFinalResult>> PurchaseAsync(string internalProductId, CancellationToken ct = default) => _notSupported;
        public Task<CommonResult<EntitlementsSnapshot>> RestoreAsync(CancellationToken ct = default) => _notSupportedSnapshot;
        public Task<CommonResult<EntitlementsSnapshot>> SyncEntitlementsAsync(CancellationToken ct = default) => _notSupportedSnapshot;
        public Task<CommonResult<RecentPurchaseItem>> GetLatestConsumablePurchase30dAsync(CancellationToken ct = default) => _notSupportedRecent;
        public Task<CommonResult<RentalPurchaseItem>> GetLatestRentalPurchase30dAsync(CancellationToken ct = default) => _notSupportedRental;
#endif

        // ── Helpers ───────────────────────────────────────────────

        static EntitlementsSnapshot ParseEntitlementsSnapshot(Dictionary<string, object> snap)
        {
            var noAds = snap.TryGetValue("noAdsActive", out var na) && na is bool b && b;

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

            return new EntitlementsSnapshot(noAds, seasonPasses, balances);
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

        static string TryExtractStoreProductIdFromReceipt(string receipt)
        {
            if (string.IsNullOrEmpty(receipt))
                return string.Empty;

            const string plainMarker = "\"productId\":\"";
            var plainIndex = receipt.IndexOf(plainMarker, StringComparison.Ordinal);
            if (plainIndex >= 0)
            {
                var start = plainIndex + plainMarker.Length;
                var end = receipt.IndexOf('"', start);
                if (end > start)
                    return receipt.Substring(start, end - start);
            }

            const string escapedMarker = "\\\"productId\\\":\\\"";
            var escapedIndex = receipt.IndexOf(escapedMarker, StringComparison.Ordinal);
            if (escapedIndex >= 0)
            {
                var start = escapedIndex + escapedMarker.Length;
                var end = receipt.IndexOf("\\\"", start, StringComparison.Ordinal);
                if (end > start)
                    return receipt.Substring(start, end - start);
            }

            return string.Empty;
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
            public VerifyPurchaseResponse(string resultStatus, string rejectReason, EntitlementsSnapshot? snapshot)
            {
                ResultStatus = resultStatus;
                RejectReason = rejectReason;
                Snapshot = snapshot;
            }

            public string ResultStatus { get; }
            public string RejectReason { get; }
            public EntitlementsSnapshot? Snapshot { get; }
        }

        public readonly struct EntitlementsSnapshot
        {
            public EntitlementsSnapshot(bool noAdsActive, IReadOnlyList<string> ownedSeasonPasses, IReadOnlyDictionary<string, long> currencyBalances)
            {
                NoAdsActive = noAdsActive;
                OwnedSeasonPasses = ownedSeasonPasses;
                CurrencyBalances = currencyBalances;
            }

            public bool NoAdsActive { get; }
            public IReadOnlyList<string> OwnedSeasonPasses { get; }
            public IReadOnlyDictionary<string, long> CurrencyBalances { get; }
        }

        public readonly struct PurchaseFinalResult
        {
            public PurchaseFinalResult(string internalProductId, PurchaseKind kind, string resultStatus)
            {
                InternalProductId = internalProductId;
                Kind = kind;
                ResultStatus = resultStatus;
            }

            public string InternalProductId { get; }
            public PurchaseKind Kind { get; }
            public string ResultStatus { get; }
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
