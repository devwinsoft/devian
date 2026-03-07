using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Devian;
using Devian.Domain.Common;

public class TestSceneLoading : TestSceneBootstrap
{
    public static TestSceneLoading Instance => Singleton.Create<TestSceneLoading>();
    
    protected override Task onEnter()
    {
        return Task.CompletedTask;
    }

    protected override Task onExit()
    {
        return Task.CompletedTask;
    }

    protected override async Task onStart()
    {
        Debug.Log("TestSceneLoading...");
        Debug.Log(Application.persistentDataPath);
        await base.onStart();

        UICanvasLoading.Instance.Init();

        var restore = await AccountManager.Instance.EnsureRuntimeSessionAsync(CancellationToken.None);
        Debug.Log($"EnsureRuntimeSessionAsync: success={restore.IsSuccess} snapshot={restore.Value?.GetType().Name ?? "null"}");

        if (restore.IsFailure)
        {
            Debug.LogError(
                $"Runtime session restore failed: code={restore.Error.Code}, message={restore.Error.Message}");
            UICanvasLoading.Instance.message.text = $"{restore.Error.Code}";
            UICanvasLoading.Instance.ShowLoginButtons();
            return;
        }

        if (restore.Value == null)
        {
            var loginType = AccountManager.Instance.CurrentLoginType;
            var canProceedWithoutSnapshot = loginType != LoginType.NONE &&
                                            AccountManager.Instance.IsLocalOnlySaveMode;
            if (!canProceedWithoutSnapshot)
            {
                Debug.LogWarning($"Runtime session restore skipped. loginType={loginType}");
                UICanvasLoading.Instance.message.text = $"Restore required: {loginType}";
                UICanvasLoading.Instance.ShowLoginButtons();
                return;
            }

            Debug.Log(
                $"Proceeding without SessionInitSnapshot for local-only loginType={loginType}.");
        }

        var code = await syncAndInitializeAsync(restore.Value, CancellationToken.None);
        if (code != CommonErrorType.SUCCESS)
        {
            UICanvasLoading.Instance.message.text = $"{code}";
            UICanvasLoading.Instance.ShowLoginButtons();
            return;
        }

        await SceneTransManager.Instance.LoadSceneAsync("SceneSample");
    }


    public async Task<CommonErrorType> LoginSessionAsync(LoginType loginType)
    {
        var login = await AccountManager.Instance.LoginAsync(loginType, CancellationToken.None);
        Debug.Log($"LoginAsync: {loginType}, {(login.IsFailure ? login.Error?.ToString() : "")}");

        if (login.IsFailure)
        {
            Debug.LogError($"SignIn failed: code={login.Error.Code}, message={login.Error.Message}");
            return login.Error.Code;
        }

        return await syncAndInitializeAsync(login.Value, CancellationToken.None);
    }

    async Task<CommonErrorType> syncAndInitializeAsync(
        SessionInitSnapshot? snapshot, CancellationToken ct)
    {
        // sign-in/restore 이후 data-sync는 항상 먼저 수행한다.
        var sync = await SaveDataManager.Instance.SyncGameStorageAsync(ct);
        if (sync.IsFailure)
        {
            Debug.LogError($"SyncGameStorageAsync failed: {sync.Error.Code}: {sync.Error.Message}");
            return sync.Error.Code;
        }

        Debug.Log($"Sync completed: {sync.Value.State}");
        if (sync.Value.State == SyncState.Conflict)
        {
            Debug.LogWarning("Sync conflict detected. ResolveConflictAsync is required.");
            return CommonErrorType.SAVEDATA_SYNC_RESOLVE_FAILED;
        }

        if (sync.Value.State == SyncState.Initial)
        {
            var firstInit = await InventoryManager.Instance.FirstInitAsync(ct);
            if (firstInit.IsFailure)
            {
                Debug.LogError($"FirstInitAsync failed: code={firstInit.Error.Code}, message={firstInit.Error.Message}");
                return firstInit.Error.Code;
            }

            // 초기 지급 직후 즉시 저장하여 이후 단계 실패 시 지급 손실을 방지한다.
            var saveInit = await SaveDataManager.Instance.SaveGameStorageAsync(true, ct);
            if (saveInit.IsFailure)
            {
                Debug.LogError($"Initial save failed: code={saveInit.Error.Code}, message={saveInit.Error.Message}");
                return saveInit.Error.Code;
            }
        }

        return await syncGameStateAsync(snapshot, ct);
    }
    

    async Task<CommonErrorType> syncGameStateAsync(
        SessionInitSnapshot? snapshot = null, CancellationToken ct = default)
    {
        var sync = await PurchaseManager.Instance.SyncAsync(snapshot);
        if (sync.IsFailure)
        {
            Debug.LogWarning($"Purchase sync failed: {sync.Error}");
            return CommonErrorType.SUCCESS;
        }

        var result = sync.Value;

        if (result.RetryInterruptedPurchase.HasValue)
        {
            var retry = result.RetryInterruptedPurchase.Value;
            if (retry.Status == PurchaseManager.RetryInterruptedPurchaseStatus.Retried)
            {
                Debug.Log($"Interrupted purchase recovered: {retry.InternalProductId}");
            }
        }
        else if (result.RetryInterruptedError != null)
        {
            Debug.LogWarning($"RetryInterruptedPurchaseAsync failed: {result.RetryInterruptedError.Code}: {result.RetryInterruptedError.Message}");
        }

        if (result.Refund.HasValue)
        {
            var refund = result.Refund.Value;
            Debug.Log(
                $"Refund handled={refund.HandledAdjustmentCount} " +
                $"applied={refund.InventoryAppliedAdjustmentCount} " +
                $"noop={refund.NoOpAdjustmentCount}");
        }
        else if (result.RefundError != null)
        {
            Debug.LogWarning($"RefundAsync failed: {result.RefundError.Code}: {result.RefundError.Message}");
        }

        if (result.EntitlementsError != null)
        {
            Debug.LogWarning($"SyncEntitlementsAsync failed: {result.EntitlementsError.Code}: {result.EntitlementsError.Message}");
        }

        if (result.SaveError != null)
        {
            Debug.LogWarning($"SaveGameStorageAsync failed: {result.SaveError.Code}: {result.SaveError.Message}");
        }

        var initClock = await MissionManager.Instance.InitializeAsync(snapshot?.MissionClock);
        if (initClock.IsFailure)
        {
            Debug.LogError($"MissionManager.InitializeAsync failed: {initClock.Error.Code}: {initClock.Error.Message}");
            return initClock.Error.Code;
        }

        var initAchieve = await AchieveManager.Instance.InitializeAsync(ct);
        if (initAchieve.IsFailure)
        {
            Debug.LogError($"AchieveManager.InitializeAsync failed: {initAchieve.Error.Code}: {initAchieve.Error.Message}");
            return initAchieve.Error.Code;
        }

        // Todo: 최적화
        var save = await SaveDataManager.Instance.SaveGameStorageAsync(true, ct);
        if (save.IsFailure)
        {
            Debug.LogError($"Mission init save failed: {save.Error.Code}: {save.Error.Message}");
            return save.Error.Code;
        }

        return CommonErrorType.SUCCESS;
    }
}
