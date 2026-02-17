using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Devian.Domain.Common;

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
        bool _productsFetched;

        TaskCompletionSource<PendingOrder> _purchaseTcs;

        IPurchaseStore _purchaseStore;

        public void SetPurchaseStore(IPurchaseStore store)
        {
            _purchaseStore = store;
        }

        protected override void Awake()
        {
            base.Awake();
            initializeIap();
        }

        async void initializeIap()
        {
            if (_controller != null)
                return;

            _controller = UnityIAPServices.StoreController();

            // Event 등록
            _controller.OnProductsFetched += onProductsFetched;
            _controller.OnProductsFetchFailed += onProductsFetchFailed;
            _controller.OnPurchasePending += onPurchasePending;
            _controller.OnPurchaseFailed += onPurchaseFailed;
            _controller.OnStoreDisconnected += onStoreDisconnected;

            // 1) Connect
            try
            {
                await _controller.Connect();
                _connected = true;
                Debug.Log($"[{Tag}] Store connected.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Tag}] Store connect failed: {ex.Message}");
                return;
            }

            // 2) Catalog 기반 제품 등록
            var catalog = ProductCatalog.LoadDefaultCatalog();
            var definitions = new List<ProductDefinition>();
            foreach (var p in catalog.allProducts)
            {
                // storeSpecificId는 각 스토어별로 설정된 것이 있으면 현재 스토어에 맞는 것을 사용.
                // ProductDefinition(id, type)은 id == storeSpecificId로 처리.
                definitions.Add(new ProductDefinition(p.id, p.type));
            }

            _controller.FetchProducts(definitions);
            Debug.Log($"[{Tag}] IAP initializing... catalog products={catalog.allProducts.Count}");
        }

        bool IsInitialized() => _connected && _productsFetched;

        // ── Event Handlers ────────────────────────────────────────

        void onProductsFetched(List<Product> products)
        {
            _productsFetched = true;
            Debug.Log($"[{Tag}] Products fetched: {products.Count}");
        }

        void onProductsFetchFailed(ProductFetchFailed failure)
        {
            Debug.LogError($"[{Tag}] Products fetch failed: {failure}");
        }

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

        void OnDestroy()
        {
            if (_controller != null)
            {
                _controller.OnProductsFetched -= onProductsFetched;
                _controller.OnProductsFetchFailed -= onProductsFetchFailed;
                _controller.OnPurchasePending -= onPurchasePending;
                _controller.OnPurchaseFailed -= onPurchaseFailed;
                _controller.OnStoreDisconnected -= onStoreDisconnected;
            }
        }

        // ── Public API ─────────────────────────────────────────────

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
            if (!IsInitialized())
                return CommonResult<EntitlementsSnapshot>.Failure(CommonErrorType.IAP_NOT_INITIALIZED, "Store not initialized.");

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

        public Task<CommonResult<EntitlementsSnapshot>> SyncEntitlementsAsync(CancellationToken ct = default)
        {
            // 샘플은 백엔드 강제 연결을 하지 않는다.
            return Task.FromResult(CommonResult<EntitlementsSnapshot>.Failure(
                CommonErrorType.COMMON_SERVER,
                "Purchase backend not wired. Implement getEntitlements and replace this sample stub."
            ));
        }

        // ── Core Flow ──────────────────────────────────────────────

        async Task<CommonResult<PurchaseFinalResult>> purchaseAndVerifyAsync(
            string internalProductId, PurchaseKind kind, CancellationToken ct)
        {
            if (!IsInitialized())
                return CommonResult<PurchaseFinalResult>.Failure(CommonErrorType.IAP_NOT_INITIALIZED, "Store not initialized.");

            if (_purchaseStore == null)
                return CommonResult<PurchaseFinalResult>.Failure(CommonErrorType.COMMON_UNKNOWN, "PurchaseStore not set. Call SetPurchaseStore().");

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

            // v5: ConfirmPurchase(PendingOrder)
            _controller.ConfirmPurchase(pendingOrder);

            return CommonResult<PurchaseFinalResult>.Success(
                new PurchaseFinalResult(internalProductId, kind, response.ResultStatus, response.Grants));
        }

        Task<CommonResult<VerifyPurchaseResponse>> VerifyPurchaseAsync(
            VerifyPurchaseRequest request, CancellationToken ct)
        {
            return Task.FromResult(CommonResult<VerifyPurchaseResponse>.Failure(
                CommonErrorType.COMMON_SERVER,
                "Purchase backend not wired. Implement verifyPurchase and replace this sample stub."
            ));
        }

#else
        // ── Unity Purchasing unavailable ────────────────────────────

        public void SetPurchaseStore(IPurchaseStore store) { }

        static readonly Task<CommonResult<PurchaseFinalResult>> _notSupported =
            Task.FromResult(CommonResult<PurchaseFinalResult>.Failure(CommonErrorType.IAP_NOT_SUPPORTED, "Unity Purchasing not available."));

        static readonly Task<CommonResult<EntitlementsSnapshot>> _notSupportedSnapshot =
            Task.FromResult(CommonResult<EntitlementsSnapshot>.Failure(CommonErrorType.IAP_NOT_SUPPORTED, "Unity Purchasing not available."));

        public Task<CommonResult<PurchaseFinalResult>> PurchaseConsumableAsync(string internalProductId, CancellationToken ct = default) => _notSupported;
        public Task<CommonResult<PurchaseFinalResult>> PurchaseSubscriptionAsync(string internalProductId, CancellationToken ct = default) => _notSupported;
        public Task<CommonResult<PurchaseFinalResult>> PurchaseSeasonPassAsync(string internalProductId, CancellationToken ct = default) => _notSupported;
        public Task<CommonResult<EntitlementsSnapshot>> RestoreAsync(CancellationToken ct = default) => _notSupportedSnapshot;
        public Task<CommonResult<EntitlementsSnapshot>> SyncEntitlementsAsync(CancellationToken ct = default) => _notSupportedSnapshot;
#endif

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
            Subscription = 1,
            SeasonPass = 2,
        }
    }
}
