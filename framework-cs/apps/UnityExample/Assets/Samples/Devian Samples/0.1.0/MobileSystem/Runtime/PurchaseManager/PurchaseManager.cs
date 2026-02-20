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

        protected override void Awake()
        {
            base.Awake();
            SetProductCatalog(new GameProductCatalog());
        }

        string ResolveRewardGroupId(string internalProductId)
        {
            return TB_PRODUCT.Get(internalProductId).RewardGroupId;
        }

#if UNITY_PURCHASING
        StoreController _controller;
        bool _connected;
        bool _iapInitialized;
        string _initError;

        Task<CommonResult> _initializeTask;

        TaskCompletionSource<PendingOrder> _purchaseTcs;
        bool _purchaseInProgress;

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

        public Task<CommonResult<PurchaseFinalResult>> PurchaseConsumableAsync(
            string internalProductId, CancellationToken ct = default)
        {
            return purchaseAndVerifyAsync(internalProductId, PurchaseKind.Consumable, ct);
        }

        public Task<CommonResult<PurchaseFinalResult>> PurchaseSubscriptionAsync(
            string internalProductId, CancellationToken ct = default)
        {
            return purchaseAndVerifyAsync(internalProductId, PurchaseKind.Subscription, ct);
        }

        public Task<CommonResult<PurchaseFinalResult>> PurchaseSeasonPassAsync(
            string internalProductId, CancellationToken ct = default)
        {
            return purchaseAndVerifyAsync(internalProductId, PurchaseKind.SeasonPass, ct);
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
                    tcs.TrySetResult(CommonResult<bool>.Failure(CommonErrorType.PURCHASE_STORE_FAILED, error ?? "RestoreTransactions failed."));
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
                var callable = FirebaseFunctions.DefaultInstance.GetHttpsCallable(functionName);
                var result = data != null
                    ? await callable.CallAsync(data)
                    : await callable.CallAsync();

                ct.ThrowIfCancellationRequested();

                var response = result.Data as Dictionary<string, object>;
                if (response == null)
                    return CommonResult<Dictionary<string, object>>.Failure(
                        CommonErrorType.COMMON_SERVER, $"{functionName} returned null.");

                return CommonResult<Dictionary<string, object>>.Success(response);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                var mapped = mapFirebaseException(functionName, ex);
                if (mapped.HasValue) return mapped.Value;

                Debug.LogError($"[{Tag}] {functionName} failed: {ex.Message}");
                return CommonResult<Dictionary<string, object>>.Failure(
                    CommonErrorType.COMMON_SERVER, ex.Message);
            }
        }

        static CommonResult<Dictionary<string, object>>? mapFirebaseException(string functionName, Exception ex)
        {
            if (!(ex is FunctionsException fex)) return null;

            switch (fex.ErrorCode)
            {
                case FunctionsErrorCode.Unauthenticated:
                    return CommonResult<Dictionary<string, object>>.Failure(
                        CommonErrorType.PURCHASE_UNAUTHENTICATED, "Authentication required.");

                case FunctionsErrorCode.Unavailable:
                case FunctionsErrorCode.DeadlineExceeded:
                    return CommonResult<Dictionary<string, object>>.Failure(
                        CommonErrorType.PURCHASE_NETWORK_UNAVAILABLE, "Network unavailable.");

                default:
                    return CommonResult<Dictionary<string, object>>.Failure(
                        CommonErrorType.COMMON_SERVER, fex.Message);
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

            if (_purchaseStore == null)
                return CommonResult<PurchaseFinalResult>.Failure(CommonErrorType.COMMON_UNKNOWN, "PurchaseStore not set. Call SetPurchaseStore().");

            if (_purchaseInProgress)
                return CommonResult<PurchaseFinalResult>.Failure(CommonErrorType.PURCHASE_STORE_FAILED, "Another purchase is already in progress.");

            _purchaseInProgress = true;
            try
            {
                _purchaseTcs = new TaskCompletionSource<PendingOrder>();
                _controller.PurchaseProduct(internalProductId);

                PendingOrder pendingOrder;
                try
                {
                    pendingOrder = await _purchaseTcs.Task;
                }
                catch (Exception ex)
                {
                    return CommonResult<PurchaseFinalResult>.Failure(CommonErrorType.PURCHASE_STORE_FAILED, ex.Message);
                }
                finally
                {
                    _purchaseTcs = null;
                }

                ct.ThrowIfCancellationRequested();

                var store = _purchaseStore.StoreKey;
                var payload = _purchaseStore.BuildVerifyPayload(pendingOrder.Info.Receipt);
                var verifyResult = await verifyPurchaseAsync(internalProductId, kind, store, payload, ct);
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

                return CommonResult<PurchaseFinalResult>.Failure(
                    CommonErrorType.PURCHASE_STORE_FAILED,
                    $"Purchase verify returned non-granted status: {status}");
            }
            finally
            {
                _purchaseInProgress = false;
            }
        }

        async Task<CommonResult<VerifyPurchaseResponse>> verifyPurchaseAsync(
            string internalProductId, PurchaseKind kind, string store, string payload,
            CancellationToken ct)
        {
            var data = new Dictionary<string, object>
            {
                ["storeKey"] = store,
                ["internalProductId"] = internalProductId,
                ["kind"] = PurchaseKindToString(kind),
                ["payload"] = payload,
            };

            var result = await callFunctionAsync("verifyPurchase", data, ct);
            if (result.IsFailure)
                return CommonResult<VerifyPurchaseResponse>.Failure(result.Error!);

            var response = result.Value!;
            var resultStatus = response.TryGetValue("resultStatus", out var rs) ? rs as string ?? "" : "";

            EntitlementsSnapshot? snapshot = null;
            if (response.TryGetValue("entitlementsSnapshot", out var snapObj) && snapObj is Dictionary<string, object> snap)
                snapshot = ParseEntitlementsSnapshot(snap);

            return CommonResult<VerifyPurchaseResponse>.Success(
                new VerifyPurchaseResponse(resultStatus, snapshot));
        }

        // ── Event Handlers ────────────────────────────────────────

        void onPurchasePending(PendingOrder order)
        {
            _purchaseTcs?.TrySetResult(order);
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

        static readonly Task<CommonResult<RentalPurchaseItem>> _notSupportedRental =
            Task.FromResult(CommonResult<RentalPurchaseItem>.Failure(CommonErrorType.IAP_NOT_SUPPORTED, "Unity Purchasing not available."));

        public Task<CommonResult> InitializeAsync(CancellationToken ct = default) => _notSupportedInit;
        public Task<CommonResult<PurchaseFinalResult>> PurchaseConsumableAsync(string internalProductId, CancellationToken ct = default) => _notSupported;
        public Task<CommonResult<PurchaseFinalResult>> PurchaseSubscriptionAsync(string internalProductId, CancellationToken ct = default) => _notSupported;
        public Task<CommonResult<PurchaseFinalResult>> PurchaseSeasonPassAsync(string internalProductId, CancellationToken ct = default) => _notSupported;
        public Task<CommonResult<EntitlementsSnapshot>> RestoreAsync(CancellationToken ct = default) => _notSupportedSnapshot;
        public Task<CommonResult<EntitlementsSnapshot>> SyncEntitlementsAsync(CancellationToken ct = default) => _notSupportedSnapshot;
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
            public VerifyPurchaseResponse(string resultStatus, EntitlementsSnapshot? snapshot)
            {
                ResultStatus = resultStatus;
                Snapshot = snapshot;
            }

            public string ResultStatus { get; }
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

        public sealed class RentalPurchaseItem
        {
            public string purchaseId;
            public string internalProductId;
            public long storePurchasedAtMs;
            public string status;
        }
    }
}
