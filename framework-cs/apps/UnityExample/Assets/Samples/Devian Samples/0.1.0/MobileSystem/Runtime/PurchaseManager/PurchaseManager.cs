using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Devian.Domain.Common;
using Firebase.Functions;

#if UNITY_PURCHASING
using UnityEngine.Purchasing;
#endif

namespace Devian
{
    public sealed class PurchaseManager : CompoSingleton<PurchaseManager>
    {
#if UNITY_PURCHASING
        const string Tag = "PurchaseManager";

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

            // v5: RestoreTransactions는 StoreController에 직접 존재 (플랫폼 분기 불필요)
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

            // 최종 entitlement는 서버 getEntitlements로 확정
            return await SyncEntitlementsAsync(ct);
        }

        public async Task<CommonResult<EntitlementsSnapshot>> SyncEntitlementsAsync(CancellationToken ct = default)
        {
            try
            {
                // getEntitlements: uid는 Firebase Auth context에서 자동 전달
                var callable = FirebaseFunctions.DefaultInstance.GetHttpsCallable("getEntitlements");
                var result = await callable.CallAsync();

                ct.ThrowIfCancellationRequested();

                var response = result.Data as Dictionary<string, object>;
                if (response == null)
                    return CommonResult<EntitlementsSnapshot>.Failure(CommonErrorType.COMMON_SERVER, "getEntitlements returned null.");

                return CommonResult<EntitlementsSnapshot>.Success(ParseEntitlementsSnapshot(response));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Tag}] getEntitlements failed: {ex.Message}");
                return CommonResult<EntitlementsSnapshot>.Failure(CommonErrorType.COMMON_SERVER, ex.Message);
            }
        }

        public async Task<CommonResult<RentalPurchaseItem>> GetLatestRentalPurchase30dAsync(CancellationToken ct = default)
        {
#if UNITY_EDITOR
            return CommonResult<RentalPurchaseItem>.Failure(
                CommonErrorType.PURCHASE_UNSUPPORTED_PLATFORM,
                "PurchaseManager is not supported in Editor."
            );
#endif
            if (!_iapInitialized)
                return CommonResult<RentalPurchaseItem>.Failure(
                    CommonErrorType.PURCHASE_INIT_REQUIRED,
                    "PurchaseManager not initialized. Call InitializeAsync() first.");

            try
            {
                // 서버가 "최근 30일"을 계산한다. 클라/기기 시간 사용 금지.
                var data = new Dictionary<string, object>
                {
                    ["pageSize"] = 1
                };

                var callable = FirebaseFunctions.DefaultInstance.GetHttpsCallable("getRecentRentalPurchases30d");
                var result = await callable.CallAsync(data);

                ct.ThrowIfCancellationRequested();

                var item = parseFirstRentalPurchaseItem(result.Data);
                if (item == null)
                    return CommonResult<RentalPurchaseItem>.Failure(
                        CommonErrorType.PURCHASE_RENTAL_LATEST_NOT_FOUND,
                        "No recent rental purchase within 30 days.");

                return CommonResult<RentalPurchaseItem>.Success(item);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Firebase Functions 예외 매핑
                if (ex is Firebase.Functions.FunctionsException fex)
                {
                    switch (fex.ErrorCode)
                    {
                        case Firebase.Functions.FunctionsErrorCode.Unauthenticated:
                            return CommonResult<RentalPurchaseItem>.Failure(
                                CommonErrorType.PURCHASE_UNAUTHENTICATED,
                                "Authentication required."
                            );

                        case Firebase.Functions.FunctionsErrorCode.Unavailable:
                        case Firebase.Functions.FunctionsErrorCode.DeadlineExceeded:
                            return CommonResult<RentalPurchaseItem>.Failure(
                                CommonErrorType.PURCHASE_NETWORK_UNAVAILABLE,
                                "Network unavailable."
                            );

                        default:
                            return CommonResult<RentalPurchaseItem>.Failure(
                                CommonErrorType.PURCHASE_RENTAL_LATEST_CALL_FAILED,
                                fex.Message
                            );
                    }
                }

                Debug.LogError($"[{Tag}] getRecentRentalPurchases30d failed: {ex.Message}");
                return CommonResult<RentalPurchaseItem>.Failure(
                    CommonErrorType.PURCHASE_RENTAL_LATEST_CALL_FAILED,
                    ex.Message
                );
            }
        }

        // ── Core Flow ──────────────────────────────────────────────

        /// <summary>
        /// IAP 초기화 비동기 구현.
        /// 1) StoreController 생성 + Connect
        /// 2) IPurchaseProductCatalog 기반 제품 등록 (FetchProducts)
        /// 3) FetchProducts 콜백을 TCS로 await
        /// </summary>
        async Task<CommonResult> initializeIapAsync(CancellationToken ct)
        {
            if (_productCatalog == null)
                return CommonResult.Failure(CommonErrorType.PURCHASE_INIT_FAILED, "ProductCatalog not set. Call SetProductCatalog().");

            try
            {
                _controller = UnityIAPServices.StoreController();

                // Event 등록 (Purchase 전용 — FetchProducts 콜백은 TCS 내부에서 처리)
                _controller.OnPurchasePending += onPurchasePending;
                _controller.OnPurchaseFailed += onPurchaseFailed;
                _controller.OnStoreDisconnected += onStoreDisconnected;

                // 1) Connect
                await _controller.Connect();
                _connected = true;
                Debug.Log($"[{Tag}] Store connected.");

                ct.ThrowIfCancellationRequested();

                // 2) IPurchaseProductCatalog 기반 제품 등록 (Port: 컨텐츠 레이어에서 주입)
                var items = _productCatalog.GetActiveProducts();
                var definitions = new List<ProductDefinition>(items.Count);
                foreach (var item in items)
                    definitions.Add(new ProductDefinition(item.InternalProductId, item.StoreSku, toUnityProductType(item.ProductType)));

                // 3) FetchProducts + TCS로 콜백 대기
                var fetchTcs = new TaskCompletionSource<bool>();

                void onFetched(List<Product> fetched)
                {
                    fetchTcs.TrySetResult(true);
                }

                void onFetchFailed(ProductFetchFailed failure)
                {
                    fetchTcs.TrySetException(new Exception($"Products fetch failed: {failure}"));
                }

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
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _initError = ex.Message;
                Debug.LogError($"[{Tag}] IAP initialization failed: {ex.Message}");

                // FetchProducts 실패와 그 외 초기화 실패를 구분
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

                // v5: receipt는 PendingOrder.Info.Receipt
                var store = _purchaseStore.StoreKey;
                var payload = _purchaseStore.BuildVerifyPayload(pendingOrder.Info.Receipt);
                var request = new VerifyPurchaseRequest(internalProductId, kind, store, payload);

                var verifyResult = await VerifyPurchaseAsync(request, ct);
                if (verifyResult.IsFailure)
                    return CommonResult<PurchaseFinalResult>.Failure(verifyResult.Error!);

                var response = verifyResult.Value!;
                var status = response.ResultStatus;

                // SSOT 하드룰: resultStatus에 따라 ConfirmPurchase 분기
                // - GRANTED / ALREADY_GRANTED → Confirm 실행
                // - REJECTED / PENDING / REVOKED / REFUNDED → Confirm 하지 않음
                if (status == "GRANTED" || status == "ALREADY_GRANTED")
                {
                    _controller.ConfirmPurchase(pendingOrder);

                    return CommonResult<PurchaseFinalResult>.Success(
                        new PurchaseFinalResult(internalProductId, kind, status, response.Grants));
                }

                // PENDING/REJECTED/REVOKED/REFUNDED → Failure 반환 (호출자가 IsSuccess로 지급 판단)
                // Confirm 하지 않으면 Unity IAP가 다음 앱 실행 시 OnPurchasePending을 재전달한다.
                return CommonResult<PurchaseFinalResult>.Failure(
                    CommonErrorType.PURCHASE_STORE_FAILED,
                    $"Purchase verify returned non-granted status: {status}");
            }
            finally
            {
                _purchaseInProgress = false;
            }
        }

        async Task<CommonResult<VerifyPurchaseResponse>> VerifyPurchaseAsync(
            VerifyPurchaseRequest request, CancellationToken ct)
        {
            try
            {
                // SSOT 필드 매핑: C# → Callable JSON
                var data = new Dictionary<string, object>
                {
                    ["storeKey"] = request.Store,
                    ["internalProductId"] = request.InternalProductId,
                    ["kind"] = PurchaseKindToString(request.Kind),
                    ["payload"] = request.Payload,
                };

                var callable = FirebaseFunctions.DefaultInstance.GetHttpsCallable("verifyPurchase");
                var result = await callable.CallAsync(data);

                ct.ThrowIfCancellationRequested();

                var response = result.Data as Dictionary<string, object>;
                if (response == null)
                    return CommonResult<VerifyPurchaseResponse>.Failure(CommonErrorType.COMMON_SERVER, "verifyPurchase returned null.");

                var resultStatus = response.TryGetValue("resultStatus", out var rs) ? rs as string ?? "" : "";

                // grants[] 파싱
                var grants = new List<PurchaseGrant>();
                if (response.TryGetValue("grants", out var grantsObj) && grantsObj is IEnumerable<object> grantList)
                {
                    foreach (var item in grantList)
                    {
                        if (item is Dictionary<string, object> g)
                        {
                            var type = g.TryGetValue("type", out var t) ? t as string ?? "" : "";
                            var id = g.TryGetValue("id", out var i) ? i as string ?? "" : "";
                            var amount = g.TryGetValue("amount", out var a) ? Convert.ToInt64(a) : 0L;
                            grants.Add(new PurchaseGrant(type, id, amount));
                        }
                    }
                }

                // entitlementsSnapshot (optional) 파싱
                EntitlementsSnapshot? snapshot = null;
                if (response.TryGetValue("entitlementsSnapshot", out var snapObj) && snapObj is Dictionary<string, object> snap)
                {
                    snapshot = ParseEntitlementsSnapshot(snap);
                }

                return CommonResult<VerifyPurchaseResponse>.Success(
                    new VerifyPurchaseResponse(resultStatus, grants, snapshot));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Tag}] verifyPurchase failed: {ex.Message}");
                return CommonResult<VerifyPurchaseResponse>.Failure(CommonErrorType.COMMON_SERVER, ex.Message);
            }
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

        // ── Rental Parse Helpers (private, Devian 네이밍 정책: 소문자 시작) ──

        static RentalPurchaseItem parseFirstRentalPurchaseItem(object data)
        {
            var root = data as IDictionary<string, object>;
            if (root == null) return null;

            if (!root.TryGetValue("items", out var itemsObj)) return null;

            var items = itemsObj as IList<object>;
            if (items == null || items.Count == 0) return null;

            var first = items[0] as IDictionary<string, object>;
            if (first == null) return null;

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
            public VerifyPurchaseResponse(string resultStatus, IReadOnlyList<PurchaseGrant> grants, EntitlementsSnapshot? snapshot)
            {
                ResultStatus = resultStatus;
                Grants = grants;
                Snapshot = snapshot;
            }

            public string ResultStatus { get; }
            public IReadOnlyList<PurchaseGrant> Grants { get; }
            public EntitlementsSnapshot? Snapshot { get; }
        }

        public readonly struct PurchaseGrant
        {
            public PurchaseGrant(string type, string id, long amount)
            {
                Type = type;
                Id = id;
                Amount = amount;
            }

            public string Type { get; }
            public string Id { get; }
            public long Amount { get; }
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
            public PurchaseFinalResult(string internalProductId, PurchaseKind kind, string resultStatus, IReadOnlyList<PurchaseGrant> grants)
            {
                InternalProductId = internalProductId;
                Kind = kind;
                ResultStatus = resultStatus;
                Grants = grants;
            }

            public string InternalProductId { get; }
            public PurchaseKind Kind { get; }
            public string ResultStatus { get; }
            public IReadOnlyList<PurchaseGrant> Grants { get; }
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
