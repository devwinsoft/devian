using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Devian;
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
    

    public void OnClick_Connect()
    {
        GameNetManager.Instance.Connect("ws://localhost:8080");
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
                TestSaveData data = new TestSaveData();
                data.name = "devian framework";
                data.items.Add(new TestSaveData.TestItemData());
                data.items[0].item_id = "devian item 001";
                var init = await SaveDataManager.Instance.SaveDataLocalAndCloudAsync("main", data, source.Token);
                Debug.Log($"SaveDataLocalAndCloud: {init.Value}");
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
                break;
        }
    }
    
    public void OnClick_DisConnect()
    {
        //GameNetManager.Instance.Disconnect();
        SaveDataManager.Instance.ClearSlotAsync("main", CancellationToken.None);
    }
}
