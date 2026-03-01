using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Devian;
using Devian.Domain.Common;
using NUnit.Framework.Internal;

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
        await base.onStart();
        
        UICanvasLoading.Instance.Init();
        
        var sync = await SaveDataManager.Instance.SyncAsync("main", CancellationToken.None);
        if (sync.IsSuccess)
        {
            Debug.Log($"Sync completed: {sync.Value.State}");

            switch (sync.Value.State)
            {
                case SyncState.Initial:
                    var data = SaveDataManager.Instance.ToJson();
                    var init = await SaveDataManager.Instance.SaveDataAsync("main", data, includeCloud: false, CancellationToken.None);
                    UICanvasLoading.Instance.ShowLoginButtons();
                    break;
                case SyncState.Conflict:
                    break;
                case SyncState.Success:
                    var loginCode = await Login(AccountManager.Instance.Storage.loginType, false);
                    if (loginCode == CommonErrorType.SUCCESS)
                    {
                        SceneTransManager.Instance.LoadSceneAsync("SceneSample");
                    }
                    break;
            }
        }
        else if (sync.IsFailure)
        {
            Debug.Log($"{sync.Error.Code}: {sync.Error.Message}");
        }
    }
    
    
    public async Task<CommonErrorType> Login(LoginType loginType, bool saveData)
    {
        var login = await AccountManager.Instance.LoginAsync(loginType, CancellationToken.None);
        Debug.Log($"LoginAsync: {loginType}, {(login.IsFailure ? login.Error?.ToString() : "")}");
        
        if (login.IsFailure)
        {
            Debug.LogError($"SignIn failed: code={login.Error.Code}, message={login.Error.Message}");
            return login.Error.Code;
        }
        
#if !UNITY_EDITOR
        await initPurchase();
#endif
        await SaveDataManager.Instance.SaveDataAsync("main",
            SaveDataManager.Instance.ToJson(),
            true,
            CancellationToken.None);
        return CommonErrorType.SUCCESS;
    }
    

    async Task<CommonErrorType> initPurchase()
    {
        var ct = CancellationToken.None;
        
        // 1. IAP 초기화
        var initResult = await PurchaseManager.Instance.InitializeAsync(ct);
        if (initResult.IsFailure)
        {
            Debug.LogError($"IAP init failed: {initResult.Error}");
            return initResult.Error.Code;
        }

        // 2. 중단 구매 복구
        var retryResult = await PurchaseManager.Instance.RetryInterruptedPurchaseAsync(ct);
        if (retryResult.IsSuccess)
        {
            var retry = retryResult.Value;
            if (retry.Status == PurchaseManager.RetryInterruptedPurchaseStatus.Retried)
            {
                Debug.Log($"Interrupted purchase recovered: {retry.InternalProductId}");
            }
        }
        else
        {
            Debug.LogWarning($"RetryInterruptedPurchaseAsync failed: {retryResult.Error.Code}: {retryResult.Error.Message}");
        }

        // 3. 환불 처리
        var refund = await PurchaseManager.Instance.RefundAsync(ct);
        if (refund.IsSuccess)
        {
            Debug.Log(
                $"Refund handled={refund.Value.HandledAdjustmentCount} " +
                $"applied={refund.Value.InventoryAppliedAdjustmentCount} " +
                $"noop={refund.Value.NoOpAdjustmentCount}");
        }
        else
        {
            Debug.LogWarning($"RefundAsync failed: {refund.Error.Code}: {refund.Error.Message}");
        }
        
        return CommonErrorType.SUCCESS;
    }
}
