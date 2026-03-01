using System;
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


public class UICanvasSample : UICanvas<UICanvasSample>
{
    protected override void onAwake()
    {
    }
    
    public void OnClick_SignIn_Google()
    {
        runUiTask(OnClickSignInGoogleAsync(), nameof(OnClick_SignIn_Google));
    }

    public void OnClick_Logout()
    {
        runUiTask(OnClickLogoutAsync(), nameof(OnClick_Logout));
    }

    public void OnClick_Purchase_NoAds()
    {
        runUiTask(OnClickPurchaseNoAdsAsync(), nameof(OnClick_Purchase_NoAds));
    }

    public void OnClick_Purchase_Pass()
    {
        runUiTask(OnClickPurchasePassAsync(), nameof(OnClick_Purchase_Pass));
    }

    public void OnClick_Purchase_Chest()
    {
        runUiTask(OnClickPurchaseChestAsync(), nameof(OnClick_Purchase_Chest));
    }

    public void OnClick_InAppAd()
    {
        runUiTask(OnClickInAppAdAsync(), nameof(OnClick_InAppAd));
    }

    private async Task OnClickSignInGoogleAsync()
    {
        var ct = CancellationToken.None;
        using var timeout = new CancellationTokenSource(System.TimeSpan.FromSeconds(15));
        var login = await AccountManager.Instance.LoginAsync(LoginType.GOOGLE, ct);
        Debug.Log($"SignIn Google: {login.IsSuccess} {(login.IsFailure ? login.Error?.ToString() : "")}");
        if (login.IsFailure)
        {
            Debug.LogError($"SignIn failed: code={login.Error.Code}, message={login.Error.Message}");
            return;
        }

        // 1. SaveData Sync
        var sync = await SaveDataManager.Instance.SyncGameStorageAsync(timeout.Token);
        Debug.Log($"Sync state: {sync.Value?.State}");
        if (sync.IsFailure)
        {
            Debug.LogWarning($"[TestUICanvas] SyncGameStorageAsync failed: {sync.Error}");
            return;
        }

        // ── Rental 상태 확인 ──
        var noAds = InventoryManager.Instance.Storage.HasActiveRental("noads_month");
        Debug.Log($"no_ads:{noAds}");
    }
    
    private async Task OnClickLogoutAsync()
    {
        Debug.Log(Application.persistentDataPath);
        PoolManager.Instance.ClearAll();
        AccountManager.Instance.Logout();
        SoundManager.Instance.StopAll();
        var clear = await SaveDataManager.Instance.ClearSaveAsync(CancellationToken.None);
        if (clear.IsFailure)
        {
            Debug.LogWarning($"ClearSaveAsync failed: {clear.Error.Code}: {clear.Error.Message}");
        }

        await SceneTransManager.Instance.LoadSceneAsync("SceneLoading");
    }
    
    private async Task OnClickPurchaseNoAdsAsync()
    {
        Debug.Log(TestApplication.GetVersionCode());

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
        }
        foreach (var key in InventoryManager.Instance.Storage.Rentals.Keys)
        {
            Debug.LogWarning($"Rentals: {key}");
        }
    }
    
    private async Task OnClickPurchasePassAsync()
    {
        var result = await PurchaseManager.Instance.PurchaseAsync(
            "pass_001",
            CancellationToken.None);
        if (result.IsSuccess)
        {
            Debug.Log(result.Value.ResultStatus);
            foreach (var reward in result.Value.AppliedRewards)
            {
                Debug.Log($"{reward.Type}, {reward.Id}, {reward.Amount}");
            }
        }
        else
        {
            Debug.Log($"{result.Error.Code}: {result.Error.Message}");
        }
    }


    private async Task OnClickPurchaseChestAsync()
    {
        var result = await PurchaseManager.Instance.PurchaseAsync(
            "chest_003",
            CancellationToken.None);
        if (result.IsSuccess)
        {
            Debug.Log(result.Value.ResultStatus);
            foreach (var reward in result.Value.AppliedRewards)
            {
                Debug.Log($"{reward.Type}, {reward.Id}, {reward.Amount}");
            }
        }
        else
        {
            Debug.Log($"{result.Error.Code}: {result.Error.Message}");
        }
    }

    private async Task OnClickInAppAdAsync()
    {
        var remainMs = InventoryManager.Instance.Storage.GetRentalRemainingMs("NO_ADS");
        var result = await AdManager.Instance.ShowAsync("ad_rewarded_001", remainMs > 0);
        if (result.IsSuccess)
        {
            foreach (var reward in result.Value.AppliedRewards)
            {
                Debug.Log($"{reward.Type}, {reward.Id}, {reward.Amount}");
            }

            if (remainMs > 0)
            {
                var span = TimeSpan.FromMilliseconds(remainMs);
                Debug.Log($"remain ms: {span.ToString(@"d\:hh\:mm\:ss")}");
            }
        }
        else if (result.IsFailure)
        {
            Debug.LogWarning($"{result.Error.Code}: {result.Error.Message}");
        }
        
        /*
        var msg = new C2Game.Echo();
        msg.Message = "Echo Message";
        GameNetManager.Proxy.SendEcho(msg);
        */
    }

    private void runUiTask(Task task, string operation)
    {
        _ = observeUiTaskAsync(task, operation);
    }

    private static async Task observeUiTaskAsync(Task task, string operation)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            Debug.LogError($"UICanvasSample.{operation} failed: {ex}");
        }
    }
}
