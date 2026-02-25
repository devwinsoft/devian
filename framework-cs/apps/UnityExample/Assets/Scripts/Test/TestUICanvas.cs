using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Devian;
using Devian.Domain.Game;
using Devian.Protocol.Game;
#if UNITY_ANDROID && !UNITY_EDITOR
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif


public class TestUICanvas : UICanvas<TestUICanvas>
{
    protected override void onAwake()
    {
    }

    public async void OnClick_SignIn_Guest()
    {
        var upgrade = await AccountManager.Instance.LoginAsync(LoginType.GoogleLogin, CancellationToken.None);
        Debug.Log($"SignIn Guest: {upgrade.IsSuccess} {(upgrade.IsFailure ? upgrade.Error : "")}");
        if (upgrade.IsSuccess)
        {
            var sync = await SaveDataManager.Instance.SyncAsync(CancellationToken.None);
            Debug.Log($"Sync: {sync.IsSuccess}");
        }
    }
    
    public async void OnClick_SignIn_Apple()
    {
        var login = await AccountManager.Instance.LoginAsync(LoginType.AppleLogin, CancellationToken.None);
        Debug.Log($"SignIn Apple: {login.IsSuccess}");
    }
    
    public async void OnClick_SignIn_Google()
    {
        var ct = CancellationToken.None;
        var timeout = new CancellationTokenSource(System.TimeSpan.FromSeconds(15));
        var login = await AccountManager.Instance.LoginAsync(LoginType.GoogleLogin, ct);
        Debug.Log($"SignIn Google: {login.IsSuccess} {(login.IsFailure ? login.Error?.ToString() : "")}");
        if (login.IsFailure)
        {
            Debug.LogError($"SignIn failed: code={login.Error.Code}, message={login.Error.Message}");
            return;
        }

        // 1. SaveData Sync
        var sync = await SaveDataManager.Instance.SyncAsync("main", timeout.Token);
        Debug.Log($"Sync state: {sync.Value?.State}");
        if (sync.IsFailure)
        {
            Debug.LogWarning($"[TestUICanvas] SyncAsync failed: {sync.Error}");
            return;
        }
        
        Debug.Log($"Sync success: {sync.Value.State}");
        switch (sync.Value.State)
        {
            case SyncState.Initial:
            {
                var data = GameStorageManager.Instance.ToJson();
                var init = await SaveDataManager.Instance.SaveDataAsync("main", data, includeCloud: true, timeout.Token);
                Debug.Log($"SaveData: {data}");
                break;
            }
            case SyncState.Conflict:
                var resolve = await SaveDataManager.Instance.ResolveConflictAsync("main", SyncResolution.UseLocal, timeout.Token);
                Debug.Log($"ResolveConflict: {resolve.Value}");
                break;
            case SyncState.ConnectionFailed:
                Debug.LogWarning("[TestUICanvas] Cloud connection failed and no local data exists. Retry or check connection.");
                return;
            default:
                Debug.Log($"LocalPayload: {sync.Value.LocalPayload?.payload}");
                Debug.Log($"CloudPayload: {sync.Value.CloudPayload?.Payload}");
                break;
        }

        // 4. IAP 초기화
        var initResult = await PurchaseManager.Instance.InitializeAsync(ct);
        if (initResult.IsFailure)
        {
            Debug.LogError($"IAP init failed: {initResult.Error}");
            return;
        }

        // 5. 중단 구매 복구
        var retryResult = await PurchaseManager.Instance.RetryInterruptedPurchaseAsync(ct);
        if (retryResult.IsSuccess)
        {
            var retry = retryResult.Value;
            if (retry.Status == PurchaseManager.RetryInterruptedPurchaseStatus.Retried)
            {
                Debug.Log($"Interrupted purchase recovered: {retry.InternalProductId}");
            }
        }

        // 6. 환불 처리
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

        // ── Rental 상태 확인 ──
        var noAds = GameStorageManager.Instance.Inventory.HasActiveRental("noads_month");
        Debug.Log($"no_ads:{noAds}");
    }
    

    public void OnClick_Logout()
    {
        Debug.Log(Application.persistentDataPath);
        AccountManager.Instance.Logout();
        Debug.Log($"Logout");
    }
    

    public async void OnClick_Purchase_1()
    {
        Debug.Log(TestBootstrap.GetVersionCode());

        var retry = await PurchaseManager.Instance.RetryInterruptedPurchaseAsync(CancellationToken.None);
        if (retry.IsSuccess)
        {
            Debug.Log($"RetryInterruptedPurchaseAsync: {retry.Value.Status}");
            foreach (var reward in retry.Value.AppliedRewards)
            {
                Debug.Log($"{reward.Type}, {reward.Id}, {reward.Amount}");
            }

            if (retry.Value.Status == PurchaseManager.RetryInterruptedPurchaseStatus.Retried)
            {
                Debug.Log($"Interrupted purchase recovered. product={retry.Value.InternalProductId} result={retry.Value.ResultStatus}");
                return;
            }
        }
        else
        {
            Debug.LogWarning($"RetryInterruptedPurchaseAsync failed: {retry.Error.Code}: {retry.Error.Message}");
        }

        var purchase = await PurchaseManager.Instance.PurchaseAsync(
            "noads_month",
            CancellationToken.None);
        if (purchase.IsSuccess)
        {
            Debug.Log(purchase.Value.ResultStatus);
        }
        else
        {
            Debug.Log($"{purchase.Error.Code}: {purchase.Error.Message}");
            return;
        }
    }


    public async void OnClick_Purchase_2()
    {
        Debug.Log(TestBootstrap.GetVersionCode());

        var retry = await PurchaseManager.Instance.RetryInterruptedPurchaseAsync(CancellationToken.None);
        if (retry.IsSuccess)
        {
            Debug.Log($"RetryInterruptedPurchaseAsync: {retry.Value.Status}");

            if (retry.Value.Status == PurchaseManager.RetryInterruptedPurchaseStatus.Retried)
            {
                Debug.Log($"Interrupted purchase recovered. product={retry.Value.InternalProductId} result={retry.Value.ResultStatus}");
                foreach (var reward in retry.Value.AppliedRewards)
                {
                    Debug.Log($"{reward.Type}, {reward.Id}, {reward.Amount}");
                }
                return;
            }
        }
        else
        {
            Debug.LogWarning($"RetryInterruptedPurchaseAsync failed: {retry.Error.Code}: {retry.Error.Message}");
        }

        var purchase = await PurchaseManager.Instance.PurchaseAsync(
            "chest_003",
            CancellationToken.None);
        if (purchase.IsSuccess)
        {
            Debug.Log(purchase.Value.ResultStatus);
            foreach (var reward in purchase.Value.AppliedRewards)
            {
                Debug.Log($"{reward.Type}, {reward.Id}, {reward.Amount}");
            }
        }
        else
        {
            Debug.Log($"{purchase.Error.Code}: {purchase.Error.Message}");
            return;
        }
    }
    
    
    public async void OnClick_Echo()
    {
        /*
        var msg = new C2Game.Echo();
        msg.Message = "Echo Message";
        GameNetManager.Proxy.SendEcho(msg);
        */
    }
    
    public void OnClick_DisConnect()
    {
        //GameNetManager.Instance.Disconnect();
        SaveDataManager.Instance.ClearSlotAsync("main", CancellationToken.None);
    }
}
