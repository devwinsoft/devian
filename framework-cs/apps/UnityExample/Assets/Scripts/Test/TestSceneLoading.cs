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
        
        var sync = SaveDataManager.Instance.SyncAsync(CancellationToken.None);
        if (sync.Result.IsSuccess)
        {
            Debug.Log($"Sync completed: {sync.Result.Value.State}");

            switch (sync.Result.Value.State)
            {
                case SyncState.Initial:
                    UICanvasLoading.Instance.ShowLoginButtons();
                    break;
                case SyncState.Conflict:
                    break;
                case SyncState.Success:
                    var loginResult = await Login(GameStorageManager.Instance.Account.loginType);
                    SceneTransManager.Instance.LoadSceneAsync("SceneSample");
                    break;
            }
        }
        else
        {
            Debug.Log($"{sync.Result.Error.Code}: {sync.Result.Error.Message}");
        }
    }
    
    
    public async Task<CommonErrorType> Login(LoginType loginType)
    {
        var login = await AccountManager.Instance.LoginAsync(loginType, CancellationToken.None);
        Debug.Log($"LoginAsync: {loginType}, {(login.IsFailure ? login.Error?.ToString() : "")}");
        if (login.IsFailure)
        {
            Debug.LogError($"SignIn failed: code={login.Error.Code}, message={login.Error.Message}");
            return login.Error.Code;
        }
#if UNITY_EDITOR
        return CommonErrorType.SUCCESS;
#else
        return await initPurchase();
#endif
    }
    

    async Task<CommonErrorType> initPurchase()
    {
        var ct = CancellationToken.None;
        
        // 1. IAP 초기화
        var initIAP = await PurchaseManager.Instance.InitializeAsync(ct);
        if (initIAP.IsFailure)
        {
            Debug.LogError($"IAP init failed: {initIAP.Error}");
            return initIAP.Error.Code;
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
