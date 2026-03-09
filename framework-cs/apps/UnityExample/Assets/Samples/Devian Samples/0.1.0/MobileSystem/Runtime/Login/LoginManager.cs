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
        public bool IsConflict => SyncState == SyncState.Conflict;

        public LoginInitializeResult(
            SyncState syncState,
            string localDeviceId = "",
            string cloudDeviceId = "",
            SaveRecordSummary localSummary = null,
            SaveRecordSummary cloudSummary = null)
        {
            SyncState = syncState;
            LocalSummary = localSummary ?? SaveRecordSummary.Missing();
            CloudSummary = cloudSummary ?? SaveRecordSummary.Missing();
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

        public async Task<CommonResult<LoginInitializeResult>> EnsureRuntimeSessionAndInitializeAsync(CancellationToken ct = default)
        {
            var restore = await AccountManager.Instance.EnsureRuntimeAuthSessionAsync(ct);
            await yieldMainThreadAsync(ct);
            if (restore.IsFailure)
                return CommonResult<LoginInitializeResult>.Failure(restore.Error!);

            SessionInitSnapshot? snapshot = null;
            if (restore.Value)
            {
                var initSession = await initializeSessionSnapshotAsync(ct);
                await yieldMainThreadAsync(ct);
                if (initSession.IsFailure)
                    return CommonResult<LoginInitializeResult>.Failure(initSession.Error!);

                snapshot = initSession.Value;
            }
            else
            {
                var loginType = AccountManager.Instance.CurrentLoginType;
                if (loginType == LoginType.NONE)
                {
                    var localInit = await syncAndInitializeAsync(snapshot, ct);
                    await yieldMainThreadAsync(ct);
                    if (localInit.IsFailure)
                        return localInit;

                    Debug.Log($"[{Tag}] No previous login. Runtime initialized in local mode. Waiting for explicit login.");
                    return CommonResult<LoginInitializeResult>.Success(null);
                }

                var canProceedWithoutSnapshot = AccountManager.Instance.IsLocalOnlySaveMode;
                if (!canProceedWithoutSnapshot)
                {
                    var localInit = await syncAndInitializeAsync(snapshot, ct);
                    await yieldMainThreadAsync(ct);
                    if (localInit.IsFailure)
                        return localInit;

                    Debug.Log($"[{Tag}] Runtime session restore unavailable. Runtime initialized in local mode. Waiting for explicit login. loginType={loginType}");
                    return CommonResult<LoginInitializeResult>.Success(null);
                }

                Debug.Log($"[{Tag}] Proceeding without SessionInitSnapshot for local-only loginType={loginType}.");
            }

            return await syncAndInitializeAsync(snapshot, ct);
        }

        public async Task<CommonResult<LoginInitializeResult>> LoginAndInitializeAsync(LoginType loginType, CancellationToken ct = default)
        {
            var login = await AccountManager.Instance.LoginAsync(loginType, ct);
            await yieldMainThreadAsync(ct);
            if (login.IsFailure)
                return CommonResult<LoginInitializeResult>.Failure(login.Error!);

            var initSession = await initializeSessionSnapshotAsync(ct);
            await yieldMainThreadAsync(ct);
            if (initSession.IsFailure)
                return CommonResult<LoginInitializeResult>.Failure(initSession.Error!);

            return await syncAndInitializeAsync(initSession.Value, ct);
        }

        public async Task<CommonResult<LoginInitializeResult>> ResolveConflictAndInitializeAsync(
            SyncResolution resolution,
            CancellationToken ct = default)
        {
            var resolve = await SaveDataManager.Instance.ResolveConflictAsync(resolution, ct);
            await yieldMainThreadAsync(ct);
            if (resolve.IsFailure)
            {
                Debug.LogError($"[{Tag}] ResolveConflictAsync failed: {resolve.Error.Code}: {resolve.Error.Message}");
                return CommonResult<LoginInitializeResult>.Failure(resolve.Error!);
            }

            var initialize = await EnsureRuntimeSessionAndInitializeAsync(ct);
            if (initialize.IsFailure)
                return initialize;

            if (initialize.Value != null && initialize.Value.IsConflict)
            {
                return CommonResult<LoginInitializeResult>.Failure(
                    CommonErrorType.SAVEDATA_SYNC_RESOLVE_FAILED,
                    $"Sync conflict persists after resolve. local={initialize.Value.LocalDeviceId}, cloud={initialize.Value.CloudDeviceId}");
            }

            return initialize;
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

        async Task<CommonResult<SessionInitSnapshot?>> initializeSessionSnapshotAsync(CancellationToken ct)
        {
#if !UNITY_EDITOR
            var initSession = await FirebaseManager.Instance.InitSessionAsync(null, ct);
            await yieldMainThreadAsync(ct);
            if (initSession.IsFailure)
                return CommonResult<SessionInitSnapshot?>.Failure(initSession.Error!);

            return CommonResult<SessionInitSnapshot?>.Success(initSession.Value);
#else
            return CommonResult<SessionInitSnapshot?>.Success(null);
#endif
        }

        async Task<CommonResult<LoginInitializeResult>> syncAndInitializeAsync(SessionInitSnapshot? snapshot, CancellationToken ct)
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
                        sync.Value.CloudSummary));
            }

            if (sync.Value.State == SyncState.Initial)
            {
                var firstInit = await InventoryManager.Instance.FirstInitAsync(ct);
                await yieldMainThreadAsync(ct);
                if (firstInit.IsFailure)
                {
                    Debug.LogError($"[{Tag}] FirstInitAsync failed: code={firstInit.Error.Code}, message={firstInit.Error.Message}");
                    return CommonResult<LoginInitializeResult>.Failure(firstInit.Error!);
                }

                // 초기 지급 직후 즉시 저장하여 이후 단계 실패 시 지급 손실을 방지한다.
                var saveInit = await SaveDataManager.Instance.SaveGameStorageAsync(true, ct);
                await yieldMainThreadAsync(ct);
                if (saveInit.IsFailure)
                {
                    Debug.LogError($"[{Tag}] Initial save failed: code={saveInit.Error.Code}, message={saveInit.Error.Message}");
                    return CommonResult<LoginInitializeResult>.Failure(saveInit.Error!);
                }
            }

            return await syncGameStateAsync(snapshot, sync.Value, ct);
        }

        async Task<CommonResult<LoginInitializeResult>> syncGameStateAsync(SessionInitSnapshot? snapshot, SyncResult sync, CancellationToken ct)
        {
            var syncPurchase = await PurchaseManager.Instance.SyncAsync(snapshot);
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

                if (result.EntitlementsError != null)
                    Debug.LogWarning($"[{Tag}] SyncEntitlementsAsync failed: {result.EntitlementsError.Code}: {result.EntitlementsError.Message}");

                if (result.SaveError != null)
                    Debug.LogWarning($"[{Tag}] SaveGameStorageAsync failed: {result.SaveError.Code}: {result.SaveError.Message}");
            }

            var initMessage = MetaMessageManager.Instance.Initialize();
            if (initMessage.IsFailure)
            {
                Debug.LogError($"[{Tag}] MetaMessageManager.Initialize failed: {initMessage.Error.Code}: {initMessage.Error.Message}");
                return CommonResult<LoginInitializeResult>.Failure(initMessage.Error!);
            }

            var initRemoteConfig = await RemoteConfigManager.Instance.InitializeAsync(snapshot?.RemoteConfig, ct);
            await yieldMainThreadAsync(ct);
            if (initRemoteConfig.IsFailure)
            {
                Debug.LogError($"[{Tag}] RemoteConfigManager.InitializeAsync failed: {initRemoteConfig.Error.Code}: {initRemoteConfig.Error.Message}");
                return CommonResult<LoginInitializeResult>.Failure(initRemoteConfig.Error!);
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

            var initAd = await AdManager.Instance.InitializeAsync(ct);
            await yieldMainThreadAsync(ct);
            if (initAd.IsFailure)
                Debug.LogWarning($"[{Tag}] AdManager.InitializeAsync failed (non-fatal): {initAd.Error.Code}: {initAd.Error.Message}");

            var initLeaderboard = await LeaderboardManager.Instance.InitializeAsync(ct);
            await yieldMainThreadAsync(ct);
            if (initLeaderboard.IsFailure)
                Debug.LogWarning($"[{Tag}] LeaderboardManager.InitializeAsync failed (non-fatal): {initLeaderboard.Error.Code}: {initLeaderboard.Error.Message}");

            var syncSeasonReward = await LeaderboardManager.Instance.SyncSeasonTransitionRewardsAsync(ct);
            await yieldMainThreadAsync(ct);
            if (syncSeasonReward.IsFailure)
                Debug.LogWarning($"[{Tag}] LeaderboardManager.SyncSeasonTransitionRewardsAsync failed (non-fatal): {syncSeasonReward.Error.Code}: {syncSeasonReward.Error.Message}");

            var save = await SaveDataManager.Instance.SaveGameStorageAsync(true, ct);
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
                    sync.CloudSummary));
        }

        static async Task yieldMainThreadAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
        }
    }
}
