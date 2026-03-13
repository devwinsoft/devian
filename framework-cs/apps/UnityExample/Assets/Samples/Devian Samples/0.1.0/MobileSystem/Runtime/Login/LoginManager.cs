using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Common;
using UnityEngine;

namespace Devian
{
    public sealed class LoginInitializeResult
    {
        public SyncState SyncState { get; }
        public string LocalDeviceId { get; }
        public string CloudDeviceId { get; }
        public SaveRecordSummary LocalSummary { get; }
        public SaveRecordSummary CloudSummary { get; }
        public VersionCheckResult VersionResult { get; }
        public bool IsInitial => SyncState == SyncState.Initial;
        public bool IsConflict => SyncState == SyncState.Conflict;
        public bool IsRecommendUpdate => VersionResult == VersionCheckResult.RecommendUpdate;
        public bool IsForceUpdate => VersionResult == VersionCheckResult.ForceUpdate;

        public LoginInitializeResult(
            SyncState syncState,
            string localDeviceId = "",
            string cloudDeviceId = "",
            SaveRecordSummary localSummary = null,
            SaveRecordSummary cloudSummary = null,
            VersionCheckResult versionResult = VersionCheckResult.Success)
        {
            SyncState = syncState;
            LocalSummary = localSummary ?? SaveRecordSummary.Missing();
            CloudSummary = cloudSummary ?? SaveRecordSummary.Missing();
            VersionResult = versionResult;
            LocalDeviceId = !string.IsNullOrEmpty(localDeviceId)
                ? localDeviceId
                : LocalSummary.DeviceId;
            CloudDeviceId = !string.IsNullOrEmpty(cloudDeviceId)
                ? cloudDeviceId
                : CloudSummary.DeviceId;
        }
    }

    public sealed class LoginManager : CompoSingleton<LoginManager>
    {
        const string Tag = nameof(LoginManager);

        protected override void onInitAwake()
        {
        }

        public async Task<CommonResult<LoginInitializeResult>> EnsureRuntimeSessionAndInitializeAsync(
            VersionNumber clientVersion,
            CancellationToken ct = default)
        {
            var versionGate = await initializeRemoteDataAsync(clientVersion, ct);
            await yieldMainThreadAsync(ct);
            if (versionGate.IsFailure)
                return CommonResult<LoginInitializeResult>.Failure(versionGate.Error!);

            if (versionGate.Value == VersionCheckResult.ForceUpdate)
                return createVersionCheckResult(versionGate.Value);

            return await ensureRuntimeSessionAndInitializeCoreAsync(
                versionGate.Value,
                ct);
        }

        public async Task<CommonResult<LoginInitializeResult>> LoginAndInitializeAsync(
            LoginType loginType,
            VersionNumber clientVersion,
            CancellationToken ct = default)
        {
            var versionGate = await initializeRemoteDataAsync(clientVersion, ct);
            await yieldMainThreadAsync(ct);
            if (versionGate.IsFailure)
                return CommonResult<LoginInitializeResult>.Failure(versionGate.Error!);

            if (versionGate.Value == VersionCheckResult.ForceUpdate)
                return createVersionCheckResult(versionGate.Value);

            var login = await AccountManager.Instance.LoginAsync(loginType, ct);
            await yieldMainThreadAsync(ct);
            if (login.IsFailure)
                return CommonResult<LoginInitializeResult>.Failure(login.Error!);

            var initialize = await syncAndInitializeAsync(
                versionGate.Value,
                ct);
            if (initialize.IsFailure)
                return initialize;

            if (initialize.Value.IsInitial)
            {
                var firstInit = await InventoryManager.Instance.FirstInitAsync(ct);
                await yieldMainThreadAsync(ct);
                if (firstInit.IsFailure)
                {
                    Debug.LogError($"[{Tag}] FirstInitAsync failed: code={firstInit.Error.Code}, message={firstInit.Error.Message}");
                    return CommonResult<LoginInitializeResult>.Failure(firstInit.Error!);
                }

                var saveInit = await SaveDataManager.Instance.SaveGameStorageAsync(true, ct);
                await yieldMainThreadAsync(ct);
                if (saveInit.IsFailure)
                {
                    Debug.LogError($"[{Tag}] Initial save failed: code={saveInit.Error.Code}, message={saveInit.Error.Message}");
                    return CommonResult<LoginInitializeResult>.Failure(saveInit.Error!);
                }
            }

            return initialize;
        }

        public async Task<CommonResult<LoginInitializeResult>> ResolveConflictAndInitializeAsync(
            SyncResolution resolution,
            VersionNumber clientVersion,
            CancellationToken ct = default)
        {
            var versionGate = await initializeRemoteDataAsync(clientVersion, ct);
            await yieldMainThreadAsync(ct);
            if (versionGate.IsFailure)
                return CommonResult<LoginInitializeResult>.Failure(versionGate.Error!);

            if (versionGate.Value == VersionCheckResult.ForceUpdate)
                return createVersionCheckResult(versionGate.Value);

            var resolve = await SaveDataManager.Instance.ResolveConflictAsync(resolution, ct);
            await yieldMainThreadAsync(ct);
            if (resolve.IsFailure)
            {
                Debug.LogError($"[{Tag}] ResolveConflictAsync failed: {resolve.Error.Code}: {resolve.Error.Message}");
                return CommonResult<LoginInitializeResult>.Failure(resolve.Error!);
            }

            var initialize = await ensureRuntimeSessionAndInitializeCoreAsync(
                versionGate.Value,
                ct);
            if (initialize.IsFailure)
                return initialize;

            if (initialize.Value.IsConflict)
            {
                return CommonResult<LoginInitializeResult>.Failure(
                    COMMON_ERROR_TYPE.SAVEDATA_SYNC_RESOLVE_FAILED,
                    $"Sync conflict persists after resolve. local={initialize.Value.LocalDeviceId}, cloud={initialize.Value.CloudDeviceId}");
            }

            return initialize;
        }

        public Task<CommonResult<VersionCheckResult>> VersionCheck(VersionNumber clientVersion, CancellationToken ct = default)
        {
            return VersionCheckAsync(clientVersion, ct);
        }

        public async Task<CommonResult<VersionCheckResult>> VersionCheckAsync(VersionNumber clientVersion, CancellationToken ct = default)
        {
            var versionGate = await initializeRemoteDataAsync(clientVersion, ct);
            await yieldMainThreadAsync(ct);
            if (versionGate.IsFailure)
                return CommonResult<VersionCheckResult>.Failure(versionGate.Error!);

            return CommonResult<VersionCheckResult>.Success(versionGate.Value);
        }

        public bool IsPurchaseLoginReady()
        {
            try
            {
                return AccountManager.Instance.HasAuthenticatedSession;
            }
            catch
            {
                return false;
            }
        }

        public async Task<CommonResult<bool>> EnsurePurchaseLoginReadyAsync(CancellationToken ct = default)
        {
            var runtimeAuth = await AccountManager.Instance.EnsureRuntimeAuthSessionAsync(ct);
            await yieldMainThreadAsync(ct);
            if (runtimeAuth.IsFailure)
                return CommonResult<bool>.Failure(runtimeAuth.Error!);

            if (runtimeAuth.Value)
                return CommonResult<bool>.Success(true);

            if (AccountManager.Instance.CurrentLoginType != LoginType.NONE)
                return CommonResult<bool>.Success(false);

#if UNITY_ANDROID && !UNITY_EDITOR
            return await AccountManager.Instance.TryRestoreGoogleAuthAsync(ct);
#else
            return CommonResult<bool>.Success(false);
#endif
        }

        async Task<CommonResult<LoginInitializeResult>> ensureRuntimeSessionAndInitializeCoreAsync(
            VersionCheckResult versionResult,
            CancellationToken ct)
        {
            var restore = await AccountManager.Instance.EnsureRuntimeAuthSessionAsync(ct);
            await yieldMainThreadAsync(ct);
            if (restore.IsFailure)
                return CommonResult<LoginInitializeResult>.Failure(restore.Error!);

            if (!restore.Value)
            {
                var loginType = AccountManager.Instance.CurrentLoginType;
                if (loginType == LoginType.NONE)
                {
                    var localInit = await syncAndInitializeAsync(
                        versionResult,
                        ct);
                    await yieldMainThreadAsync(ct);
                    if (localInit.IsFailure)
                        return localInit;

                    Debug.Log($"[{Tag}] No previous login. Runtime initialized in local mode. Waiting for explicit login.");
                    return localInit;
                }

                if (!AccountManager.Instance.IsLocalOnlySaveMode)
                {
                    var localInit = await syncAndInitializeAsync(
                        versionResult,
                        ct);
                    await yieldMainThreadAsync(ct);
                    if (localInit.IsFailure)
                        return localInit;

                    Debug.Log($"[{Tag}] Runtime session restore unavailable. Runtime initialized in local mode. Waiting for explicit login. loginType={loginType}");
                    return localInit;
                }

                Debug.Log($"[{Tag}] Proceeding in local-only mode for loginType={loginType}.");
            }

            return await syncAndInitializeAsync(versionResult, ct);
        }

        async Task<CommonResult<LoginInitializeResult>> syncAndInitializeAsync(
            VersionCheckResult versionResult,
            CancellationToken ct)
        {
            // sign-in/restore 이후 data-sync는 항상 먼저 수행한다.
            var sync = await SaveDataManager.Instance.SyncGameStorageAsync(ct);
            await yieldMainThreadAsync(ct);
            if (sync.IsFailure)
            {
                Debug.LogError($"[{Tag}] SyncGameStorageAsync failed: {sync.Error.Code}: {sync.Error.Message}");
                return CommonResult<LoginInitializeResult>.Failure(sync.Error!);
            }

            Debug.Log($"[{Tag}] Sync completed: {sync.Value.State}");
            if (sync.Value.State == SyncState.Conflict)
            {
                Debug.LogWarning($"[{Tag}] Sync conflict detected. ResolveConflictAsync is required. local={sync.Value.LocalDeviceId}, cloud={sync.Value.CloudDeviceId}");
                return CommonResult<LoginInitializeResult>.Success(
                    new LoginInitializeResult(
                        SyncState.Conflict,
                        sync.Value.LocalDeviceId,
                        sync.Value.CloudDeviceId,
                        sync.Value.LocalSummary,
                        sync.Value.CloudSummary,
                        versionResult));
            }

            return await syncGameStateAsync(versionResult, sync.Value, ct);
        }

        async Task<CommonResult<LoginInitializeResult>> syncGameStateAsync(
            VersionCheckResult versionResult,
            SyncResult sync,
            CancellationToken ct)
        {
            var syncPurchase = await PurchaseManager.Instance.SyncAsync();
            await yieldMainThreadAsync(ct);
            if (syncPurchase.IsFailure)
            {
                Debug.LogWarning($"[{Tag}] Purchase sync failed (non-fatal): {syncPurchase.Error}");
            }
            else
            {
                var result = syncPurchase.Value;
                if (result.RetryInterruptedPurchase.HasValue)
                {
                    var retry = result.RetryInterruptedPurchase.Value;
                    if (retry.Status == PurchaseManager.RetryInterruptedPurchaseStatus.Retried)
                        Debug.Log($"[{Tag}] Interrupted purchase recovered: {retry.InternalProductId}");
                }
                else if (result.RetryInterruptedError != null)
                {
                    Debug.LogWarning($"[{Tag}] RetryInterruptedPurchaseAsync failed: {result.RetryInterruptedError.Code}: {result.RetryInterruptedError.Message}");
                }

                if (result.Refund.HasValue)
                {
                    var refund = result.Refund.Value;
                    Debug.Log(
                        $"[{Tag}] Refund handled={refund.HandledAdjustmentCount} " +
                        $"applied={refund.InventoryAppliedAdjustmentCount} " +
                        $"noop={refund.NoOpAdjustmentCount}");
                }
                else if (result.RefundError != null)
                {
                    Debug.LogWarning($"[{Tag}] RefundAsync failed: {result.RefundError.Code}: {result.RefundError.Message}");
                }

                if (result.SaveError != null)
                    Debug.LogWarning($"[{Tag}] SaveGameStorageAsync failed: {result.SaveError.Code}: {result.SaveError.Message}");
            }

            var initAttend = await AttendManager.Instance.InitializeAsync(ct);
            await yieldMainThreadAsync(ct);
            if (initAttend.IsFailure)
            {
                Debug.LogError($"[{Tag}] AttendManager.InitializeAsync failed: {initAttend.Error.Code}: {initAttend.Error.Message}");
                return CommonResult<LoginInitializeResult>.Failure(initAttend.Error!);
            }

            var initMission = await MissionManager.Instance.InitializeAsync(ct);
            await yieldMainThreadAsync(ct);
            if (initMission.IsFailure)
            {
                Debug.LogError($"[{Tag}] MissionManager.InitializeAsync failed: {initMission.Error.Code}: {initMission.Error.Message}");
                return CommonResult<LoginInitializeResult>.Failure(initMission.Error!);
            }

            var initAchieve = await AchieveManager.Instance.InitializeAsync(ct);
            await yieldMainThreadAsync(ct);
            if (initAchieve.IsFailure)
            {
                Debug.LogError($"[{Tag}] AchieveManager.InitializeAsync failed: {initAchieve.Error.Code}: {initAchieve.Error.Message}");
                return CommonResult<LoginInitializeResult>.Failure(initAchieve.Error!);
            }

            var initAd = await AdsManager.Instance.InitializeAsync(ct);
            await yieldMainThreadAsync(ct);
            if (initAd.IsFailure)
                Debug.LogWarning($"[{Tag}] AdsManager.InitializeAsync failed (non-fatal): {initAd.Error.Code}: {initAd.Error.Message}");

            var syncSeasonReward = await LeaderboardManager.Instance.SyncSeasonTransitionRewardsAsync(ct);
            await yieldMainThreadAsync(ct);
            if (syncSeasonReward.IsFailure)
                Debug.LogWarning($"[{Tag}] LeaderboardManager.SyncSeasonTransitionRewardsAsync failed (non-fatal): {syncSeasonReward.Error.Code}: {syncSeasonReward.Error.Message}");

            var initShop = ShopManager.Instance.Initialize();
            if (initShop.IsFailure)
            {
                Debug.LogError($"[{Tag}] ShopManager.Initialize failed: {initShop.Error.Code}: {initShop.Error.Message}");
                return CommonResult<LoginInitializeResult>.Failure(initShop.Error!);
            }

            var save = await SaveDataManager.Instance.SaveGameStorageAsync(false, ct);
            await yieldMainThreadAsync(ct);
            if (save.IsFailure)
            {
                Debug.LogError($"[{Tag}] SaveGameStorageAsync failed: {save.Error.Code}: {save.Error.Message}");
                return CommonResult<LoginInitializeResult>.Failure(save.Error!);
            }

            return CommonResult<LoginInitializeResult>.Success(
                new LoginInitializeResult(
                    sync.State,
                    sync.LocalDeviceId,
                    sync.CloudDeviceId,
                    sync.LocalSummary,
                    sync.CloudSummary,
                    versionResult));
        }

        async Task<CommonResult<VersionCheckResult>> initializeRemoteDataAsync(VersionNumber clientVersion, CancellationToken ct)
        {
            if (!RemoteDataManager.TryGet(out var remoteDataManager)
                || remoteDataManager == null)
            {
                return CommonResult<VersionCheckResult>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "RemoteDataManager is not initialized.");
            }

            return await remoteDataManager.InitializeAsync(clientVersion, ct);
        }

        static CommonResult<LoginInitializeResult> createVersionCheckResult(VersionCheckResult versionResult)
        {
            return CommonResult<LoginInitializeResult>.Success(
                new LoginInitializeResult(SyncState.Success, versionResult: versionResult));
        }

        static async Task yieldMainThreadAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
        }
    }
}
