using System;
using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Common;
using UnityEngine;
using UnityEngine.Networking;

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

        [Serializable]
        sealed class VersionCheckConfig
        {
            public string currentVersion = string.Empty;
            public string minVersion = string.Empty;
            public string update_url = string.Empty;
        }

        protected override void onInitAwake()
        {
        }

        public async Task<CommonResult<LoginInitializeResult>> EnsureRuntimeSessionAndInitializeAsync(
            VersionNumber clientVersion,
            CancellationToken ct = default)
        {
            var versionGate = await runVersionCheckAsync(clientVersion, ct);
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
            var versionGate = await runVersionCheckAsync(clientVersion, ct);
            await yieldMainThreadAsync(ct);
            if (versionGate.IsFailure)
                return CommonResult<LoginInitializeResult>.Failure(versionGate.Error!);

            if (versionGate.Value == VersionCheckResult.ForceUpdate)
                return createVersionCheckResult(versionGate.Value);

            var login = await AccountManager.Instance.LoginAsync(loginType, ct);
            await yieldMainThreadAsync(ct);
            if (login.IsFailure)
                return CommonResult<LoginInitializeResult>.Failure(login.Error!);

            var initSession = await initializeSessionSnapshotAsync(ct);
            await yieldMainThreadAsync(ct);
            if (initSession.IsFailure)
                return CommonResult<LoginInitializeResult>.Failure(initSession.Error!);

            return await syncAndInitializeAsync(
                initSession.Value,
                versionGate.Value,
                ct);
        }

        public async Task<CommonResult<LoginInitializeResult>> ResolveConflictAndInitializeAsync(
            SyncResolution resolution,
            VersionNumber clientVersion,
            CancellationToken ct = default)
        {
            var versionGate = await runVersionCheckAsync(clientVersion, ct);
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
            var versionGate = await runVersionCheckAsync(clientVersion, ct);
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
                    var localInit = await syncAndInitializeAsync(
                        snapshot,
                        versionResult,
                        ct);
                    await yieldMainThreadAsync(ct);
                    if (localInit.IsFailure)
                        return localInit;

                    Debug.Log($"[{Tag}] No previous login. Runtime initialized in local mode. Waiting for explicit login.");
                    return localInit;
                }

                var canProceedWithoutSnapshot = AccountManager.Instance.IsLocalOnlySaveMode;
                if (!canProceedWithoutSnapshot)
                {
                    var localInit = await syncAndInitializeAsync(
                        snapshot,
                        versionResult,
                        ct);
                    await yieldMainThreadAsync(ct);
                    if (localInit.IsFailure)
                        return localInit;

                    Debug.Log($"[{Tag}] Runtime session restore unavailable. Runtime initialized in local mode. Waiting for explicit login. loginType={loginType}");
                    return localInit;
                }

                Debug.Log($"[{Tag}] Proceeding without SessionInitSnapshot for local-only loginType={loginType}.");
            }

            return await syncAndInitializeAsync(snapshot, versionResult, ct);
        }

        async Task<CommonResult<SessionInitSnapshot?>> initializeSessionSnapshotAsync(CancellationToken ct)
        {
#if !UNITY_EDITOR
            var initSession = await FirebaseCallableManager.Instance.InitSessionAsync(null, ct);
            await yieldMainThreadAsync(ct);
            if (initSession.IsFailure)
                return CommonResult<SessionInitSnapshot?>.Failure(initSession.Error!);

            return CommonResult<SessionInitSnapshot?>.Success(initSession.Value);
#else
            return CommonResult<SessionInitSnapshot?>.Success(null);
#endif
        }

        async Task<CommonResult<LoginInitializeResult>> syncAndInitializeAsync(
            SessionInitSnapshot? snapshot,
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

            return await syncGameStateAsync(snapshot, versionResult, sync.Value, ct);
        }

        async Task<CommonResult<LoginInitializeResult>> syncGameStateAsync(
            SessionInitSnapshot? snapshot,
            VersionCheckResult versionResult,
            SyncResult sync,
            CancellationToken ct)
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
                    sync.CloudSummary,
                    versionResult));
        }

        async Task<CommonResult<VersionCheckResult>> runVersionCheckAsync(VersionNumber clientVersion, CancellationToken ct)
        {
#if UNITY_EDITOR
            _ = clientVersion;
            _ = ct;
            return CommonResult<VersionCheckResult>.Success(VersionCheckResult.Success);
#else
            var config = await fetchVersionCheckConfigAsync(ct);
            if (config.IsFailure)
                return CommonResult<VersionCheckResult>.Failure(config.Error!);

            var result = evaluateVersionCheck(config.Value, clientVersion);
            return CommonResult<VersionCheckResult>.Success(result);
#endif
        }

        async Task<CommonResult<VersionCheckConfig>> fetchVersionCheckConfigAsync(CancellationToken ct)
        {
            if (!tryResolveVersionCheckUrl(out var url))
            {
                return CommonResult<VersionCheckConfig>.Failure(
                    COMMON_ERROR_TYPE.COMMON_SERVER,
                    "Version check URL is not configured for this platform.");
            }

            using var request = UnityWebRequest.Get(url);
            request.downloadHandler = new DownloadHandlerBuffer();
            var operation = request.SendWebRequest();

            while (!operation.isDone)
            {
                if (ct.IsCancellationRequested)
                {
                    request.Abort();
                    ct.ThrowIfCancellationRequested();
                }

                await Task.Yield();
            }

            ct.ThrowIfCancellationRequested();

            if (request.result != UnityWebRequest.Result.Success)
            {
                return CommonResult<VersionCheckConfig>.Failure(
                    COMMON_ERROR_TYPE.COMMON_NETWORK,
                    $"Version config request failed: {request.error}");
            }

            var json = request.downloadHandler?.text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                return CommonResult<VersionCheckConfig>.Failure(
                    COMMON_ERROR_TYPE.COMMON_SERVER,
                    "Version config response is empty.");
            }

            try
            {
                var config = JsonUtility.FromJson<VersionCheckConfig>(json);
                if (config == null)
                {
                    return CommonResult<VersionCheckConfig>.Failure(
                        COMMON_ERROR_TYPE.COMMON_SERVER,
                        "Version config JSON parse failed.");
                }

                return CommonResult<VersionCheckConfig>.Success(config);
            }
            catch (Exception ex)
            {
                return CommonResult<VersionCheckConfig>.Failure(
                    COMMON_ERROR_TYPE.COMMON_SERVER,
                    $"Version config JSON parse failed: {ex.Message}");
            }
        }

        static bool tryResolveVersionCheckUrl(out string url)
        {
            url = string.Empty;

            var app = MobileApplication.Instance;
            if (app == null)
                return false;

#if UNITY_ANDROID
            url = app.VersionCheckAOS;
#elif UNITY_IOS
            url = app.VersionCheckIOS;
#else
            url = string.Empty;
#endif

            url = string.IsNullOrWhiteSpace(url) ? string.Empty : url.Trim();
            return !string.IsNullOrEmpty(url);
        }

        static VersionCheckResult evaluateVersionCheck(VersionCheckConfig config, VersionNumber clientVersion)
        {
            if (config == null)
                return VersionCheckResult.Success;

            var minVersionText = string.IsNullOrWhiteSpace(config.minVersion)
                ? string.Empty
                : config.minVersion.Trim();
            var currentVersionText = string.IsNullOrWhiteSpace(config.currentVersion)
                ? string.Empty
                : config.currentVersion.Trim();

            if (!string.IsNullOrEmpty(minVersionText)
                && VersionNumber.TryParse(minVersionText, out var minVersion)
                && clientVersion < minVersion)
            {
                return VersionCheckResult.ForceUpdate;
            }

            if (!string.IsNullOrEmpty(currentVersionText)
                && VersionNumber.TryParse(currentVersionText, out var recommendVersion)
                && clientVersion < recommendVersion)
            {
                return VersionCheckResult.RecommendUpdate;
            }

            return VersionCheckResult.Success;
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
