using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Devian;
using Devian.Domain.Game;
using UnityEngine.Serialization;

public class UILobbyPageCanvas : UIBasePageCanvas<UILobbyPageCanvas>
{
    [FormerlySerializedAs("menuBottom")] public UILobbyMenuBottomPanel menuBottomPanel;
    public UILobbyMenuTopPanel menuTopPanel;
    
    public UILobbyMissionPanel missionPanel;
    public UILobbyCardPanel cardPanel;
    
    protected override void onAwake()
    {
    }

    protected override void onDestroy()
    {
    }

    protected override void onInit()
    {
        base.onInit();
    }

    protected override void onInitComplete()
    {
        base.onInitComplete();
        AchieveManager.Instance.RefreshRuntimes();
        MissionManager.Instance.RefreshRuntimes();
        Debug.Log($"[UICanvasSample] remainSec={MissionManager.Instance.GetRemainTime(MISSION_TYPE.DAILY).TotalSeconds}");
    }
    
    public void OnClick_Mission_Claim()
    {
        UnityTaskRunner.Run(AchieveManager.Instance.ClaimAsync("achieve_001"), "OnClick_Mission_Claim");
    }

    public void OnClick_SignIn_Google()
    {
        UnityTaskRunner.Run(OnClickSignInGoogleAsync, $"{nameof(UILobbyPageCanvas)}.{nameof(OnClick_SignIn_Google)}");
    }

    public void OnClick_Logout()
    {
        UnityTaskRunner.Run(OnClickLogoutAsync, $"{nameof(UILobbyPageCanvas)}.{nameof(OnClick_Logout)}");
    }

    public void OnClick_InAppAd()
    {
        UnityTaskRunner.Run(OnClickInAppAdAsync, $"{nameof(UILobbyPageCanvas)}.{nameof(OnClick_InAppAd)}");
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
        var noAds = InventoryManager.Instance.HasActiveRental("purchase_noads_month");
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
    

    private async Task OnClickInAppAdAsync()
    {
        var canBuy = ShopManager.Instance.CanBuy("shop_001_ads");
        if (canBuy != GAME_ERROR_TYPE.SUCCESS)
        {
            Debug.LogWarning($"Shop CanBuy failed: {canBuy}");
            return;
        }

        var result = await ShopManager.Instance.BuyAsync("shop_001_ads");
        if (result.IsSuccess)
        {
            foreach (var reward in result.Value)
            {
                Debug.Log($"{reward.Type}, {reward.Id}, {reward.Amount}");
            }

            var remainMs = InventoryManager.Instance.GetRentalRemainingMs("NO_ADS");
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
    }
}
