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

        var sync = await SaveDataManager.Instance.SyncGameStorageAsync(CancellationToken.None);
        if (sync.IsFailure)
        {
            Debug.Log($"{sync.Error.Code}: {sync.Error.Message}");
            return;
        }

        Debug.Log($"Sync completed: {sync.Value.State}");
        switch (sync.Value.State)
        {
            case SyncState.Initial:
                var init = await SaveDataManager.Instance.SaveGameStorageAsync(CancellationToken.None);
                if (init.IsFailure)
                {
                    Debug.LogError($"Initial save failed: code={init.Error.Code}, message={init.Error.Message}");
                    UICanvasLoading.Instance.message.text = $"{init.Error.Code}";
                    return;
                }
                UICanvasLoading.Instance.ShowLoginButtons();
                break;
            
            case SyncState.Conflict:
                break;
            
            case SyncState.Success:
                var restore = await AccountManager.Instance.EnsureRuntimeSessionAsync(CancellationToken.None);
                Debug.Log($"EnsureRuntimeSessionAsync: success={restore.IsSuccess} restored={(restore.IsSuccess ? restore.Value : false)}");

                if (restore.IsFailure)
                {
                    Debug.LogError(
                        $"Runtime session restore failed: code={restore.Error.Code}, message={restore.Error.Message}");
                    UICanvasLoading.Instance.message.text = $"{restore.Error.Code}";
                    UICanvasLoading.Instance.ShowLoginButtons();
                    return;
                }

                if (!restore.Value)
                {
                    var loginType = AccountManager.Instance.CurrentLoginType;
                    Debug.LogWarning($"Runtime session restore skipped. loginType={loginType}");
                    UICanvasLoading.Instance.message.text = $"Restore required: {loginType}";
                    UICanvasLoading.Instance.ShowLoginButtons();
                    return;
                }

                var purchaseSyncCode = await syncPurchaseStateAsync();
                if (purchaseSyncCode != CommonErrorType.SUCCESS)
                {
                    UICanvasLoading.Instance.message.text = $"Purchase SyncCode: {purchaseSyncCode}";
                    UICanvasLoading.Instance.ShowLoginButtons();
                    return;
                }

                var missionInitCode = await initializeMissionAsync();
                if (missionInitCode != CommonErrorType.SUCCESS)
                {
                    Debug.LogError($"Mission initialize failed: code={missionInitCode}");
                    UICanvasLoading.Instance.message.text = $"Mission InitCode: {missionInitCode}";
                    UICanvasLoading.Instance.ShowLoginButtons();
                    return;
                }

                await SceneTransManager.Instance.LoadSceneAsync("SceneSample");
                break;
        }
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
        
#if !UNITY_EDITOR
        var purchaseSyncCode = await syncPurchaseStateAsync();
        if (purchaseSyncCode != CommonErrorType.SUCCESS)
            return purchaseSyncCode;
#endif

        var missionInitCode = await initializeMissionAsync();
        if (missionInitCode != CommonErrorType.SUCCESS)
            return missionInitCode;
        return CommonErrorType.SUCCESS;
    }
    

    async Task<CommonErrorType> syncPurchaseStateAsync()
    {
        var ct = CancellationToken.None;

        var sync = await PurchaseManager.Instance.SyncAsync(ct);
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

        return CommonErrorType.SUCCESS;
    }

    async Task<CommonErrorType> initializeMissionAsync()
    {
        var init = await MissionManager.Instance.InitializeAsync(CancellationToken.None);
        if (init.IsFailure)
        {
            Debug.LogError($"MissionManager.InitializeAsync failed: {init.Error.Code}: {init.Error.Message}");
            return init.Error.Code;
        }

        // Todo: 최적화
        var save = await SaveDataManager.Instance.SaveGameStorageAsync(CancellationToken.None);
        if (save.IsFailure)
        {
            Debug.LogError($"Mission init save failed: {save.Error.Code}: {save.Error.Message}");
            return save.Error.Code;
        }

        return CommonErrorType.SUCCESS;
    }
}
