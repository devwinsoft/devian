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

    public async void OnClick_SignIn_Apple()
    {
        var login = await AccountManager.Instance.LoginAsync(LoginType.AppleLogin, CancellationToken.None);
        Debug.Log($"SignIn Apple: {login.IsSuccess}");
    }
    
    public async void OnClick_SignIn_Google()
    {
        var login = await AccountManager.Instance.LoginAsync(LoginType.GoogleLogin, CancellationToken.None);
        Debug.Log($"SignIn Google: {login.IsSuccess} {(login.IsFailure ? login.Error?.ToString() : "")}");
        if (login.IsSuccess)
        {
            var sync = await SaveDataManager.Instance.SyncAsync(CancellationToken.None);
            Debug.Log($"Sync: {sync.IsSuccess}");

            var initResult = await PurchaseManager.Instance.InitializeAsync();
            if (initResult.IsFailure)
            {
                Debug.LogError($"IAP init failed: {initResult.Error}");
                return;
            }

            // 중단된 구매 복구 시도
            var retryResult = await PurchaseManager.Instance.RetryInterruptedPurchaseAsync();
            if (retryResult.IsSuccess)
            {
                var retry = retryResult.Value;
                if (retry.Status == PurchaseManager.RetryInterruptedPurchaseStatus.Retried)
                {
                    await HandlePurchaseClientGrantAsync(retry);
                    Debug.Log($"Interrupted purchase recovered: {retry.InternalProductId}");
                }
            }

            // 환불 처리
            var refund = await PurchaseManager.Instance.RefundAsync(CancellationToken.None);
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
        }
    }

    public async void OnClick_SignIn_Guest()
    {
        var login = await AccountManager.Instance.LoginAsync(LoginType.GuestLogin, CancellationToken.None);
        Debug.Log($"SignIn Guest: {login.IsSuccess} {(login.IsFailure ? login.Error : "")}");
        if (login.IsSuccess)
        {
            var sync = await SaveDataManager.Instance.SyncAsync(CancellationToken.None);
            Debug.Log($"Sync: {sync.IsSuccess}");
        }
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
                await HandlePurchaseClientGrantAsync(retry.Value);
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
            await HandlePurchaseClientGrantAsync(purchase.Value);
            Debug.Log(purchase.Value.ResultStatus);
        }
        else
        {
            Debug.Log($"{purchase.Error.Code}: {purchase.Error.Message}");
            return;
        }

        var source = new CancellationTokenSource(System.TimeSpan.FromSeconds(15));
        var recent = await PurchaseManager.Instance.GetLatestRentalPurchase30dAsync(source.Token);
        if (recent.IsSuccess)
        {
            Debug.Log(recent.Value.storePurchasedAtMs);
        }
        else
        {
            Debug.Log($"{recent.Error.Code}: {recent.Error.Message}");
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
                await HandlePurchaseClientGrantAsync(retry.Value);
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
            await HandlePurchaseClientGrantAsync(purchase.Value);
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
        var source = new CancellationTokenSource(System.TimeSpan.FromSeconds(15));
        var sync = await SaveDataManager.Instance.SyncAsync("main", source.Token);
        Debug.Log($"Sync state: {sync.Value?.State}");
        if (sync.IsFailure)
        {
            Debug.LogWarning($"[TestUICanvas] SyncAsync failed: {sync.Error}");
            return;
        }
        switch (sync.Value.State)
        {
            case SyncState.Initial:
            {
                var data = GameStorageManager.Instance.ToJson();
                var init = await SaveDataManager.Instance.SaveDataAsync("main", data, includeCloud: true, source.Token);
                Debug.Log($"SaveData: {init.Value}");
                break;
            }
            case SyncState.Conflict:
                var resolve = await SaveDataManager.Instance.ResolveConflictAsync("main", SyncResolution.UseLocal, source.Token);
                Debug.Log($"ResolveConflict: {resolve.Value}");
                break;
            case SyncState.ConnectionFailed:
                Debug.LogWarning("[TestUICanvas] Cloud connection failed and no local data exists. Retry or check connection.");
                return;
            default:
                Debug.Log($"Sync success: {sync.IsSuccess}");
                Debug.Log($"LocalPayload: {sync.Value.LocalPayload?.payload}");
                Debug.Log($"CloudPayload: {sync.Value.CloudPayload?.Payload}");
                if (sync.IsSuccess)
                {
                    GameStorageManager.Instance.LoadFromPayload(sync.Value.LocalPayload?.payload);
                }
                break;
        }
    }
    
    public void OnClick_DisConnect()
    {
        //GameNetManager.Instance.Disconnect();
        SaveDataManager.Instance.ClearSlotAsync("main", CancellationToken.None);
    }

    async Task HandlePurchaseClientGrantAsync(PurchaseManager.PurchaseFinalResult result)
    {
        if (!result.NeedsClientGrantDelivery)
            return;

        var apply = RewardManager.Instance.ApplyRewardDatas(result.Rewards);
        if (apply.IsSuccess)
        {
            var ack = await PurchaseManager.Instance.AckPurchaseClientGrantAppliedAsync(result.PurchaseId, CancellationToken.None);
            if (ack.IsFailure)
                Debug.LogWarning($"AckPurchaseClientGrantAppliedAsync failed: {ack.Error}");
            return;
        }

        var report = await PurchaseManager.Instance.ReportPurchaseClientGrantFailureAsync(result.PurchaseId, CancellationToken.None);
        if (report.IsFailure)
            Debug.LogWarning($"ReportPurchaseClientGrantFailureAsync failed: {report.Error}");
    }

    async Task HandlePurchaseClientGrantAsync(PurchaseManager.RetryInterruptedPurchaseResult result)
    {
        if (!result.NeedsClientGrantDelivery)
            return;

        var apply = RewardManager.Instance.ApplyRewardDatas(result.Rewards);
        if (apply.IsSuccess)
        {
            var ack = await PurchaseManager.Instance.AckPurchaseClientGrantAppliedAsync(result.PurchaseId, CancellationToken.None);
            if (ack.IsFailure)
                Debug.LogWarning($"AckPurchaseClientGrantAppliedAsync failed: {ack.Error}");
            return;
        }

        var report = await PurchaseManager.Instance.ReportPurchaseClientGrantFailureAsync(result.PurchaseId, CancellationToken.None);
        if (report.IsFailure)
            Debug.LogWarning($"ReportPurchaseClientGrantFailureAsync failed: {report.Error}");
    }
}
