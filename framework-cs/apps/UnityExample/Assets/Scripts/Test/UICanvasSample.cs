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
    
    public async void OnClick_SignIn_Google()
    {
        var ct = CancellationToken.None;
        var timeout = new CancellationTokenSource(System.TimeSpan.FromSeconds(15));
        var login = await AccountManager.Instance.LoginAsync(LoginType.GOOGLE, ct);
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

        // ── Rental 상태 확인 ──
        var noAds = GameStorageManager.Instance.Inventory.HasActiveRental("noads_month");
        Debug.Log($"no_ads:{noAds}");
    }
    

    public void OnClick_Logout()
    {
        Debug.Log(Application.persistentDataPath);
        PoolManager.Instance.ClearAll();
        AccountManager.Instance.Logout();
        SoundManager.Instance.StopAll();
        SaveDataManager.Instance.ClearSlotAsync("main", CancellationToken.None);
        SceneTransManager.Instance.LoadSceneAsync("SceneLoading");
    }
    

    public async void OnClick_Purchase_1()
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
        foreach (var key in GameStorageManager.Instance.Inventory.Rentals.Keys)
        {
            Debug.LogWarning($"Rentals: {key}");
        }
    }


    public async void OnClick_Purchase_2()
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
    
    
    public async void OnClick_InAppAd()
    {
        var remainMs = GameStorageManager.Instance.Inventory.GetRentalRemainingMs("NO_ADS");
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
                Debug.Log($"remain ms: {span.ToString("d:hh:mm:ss")}");
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

}
