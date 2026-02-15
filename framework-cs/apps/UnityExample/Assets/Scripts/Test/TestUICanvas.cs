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
        Debug.Log($"SignIn Google: {login.IsSuccess}");
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
        var sync = SaveDataManager.Instance.SyncAsync(CancellationToken.None);
        Debug.Log(sync.Result.Value.State);
        switch (sync.Result.Value.State)
        {
            case SyncState.Initial:
            {
                TestSaveData data = new TestSaveData();
                data.name = "devian framework";
                data.items.Add(new TestSaveData.TestItemData());
                data.items[0].item_id = "devian item 001";
                var init = SaveDataManager.Instance.SaveDataLocalAndCloudAsync("main", data, CancellationToken.None);
                Debug.Log(init.Result.Value);
                break;
            }
            case SyncState.Conflict:
                var resolve = SaveDataManager.Instance.ResolveConflictAsync("main", SyncResolution.UseLocal, CancellationToken.None);
                Debug.Log(resolve.Result.Value);
                break;
            case SyncState.ConnectionFailed:
                Debug.LogWarning("[TestUICanvas] Cloud connection failed and no local data exists. Retry or check connection.");
                return;
            default:
                Debug.Log(sync.Result.Value.LocalPayload);
                Debug.Log(sync.Result.Value.CloudPayload);
                break;
        }
    }
    
    public void OnClick_DisConnect()
    {
        GameNetManager.Instance.Disconnect();
    }
}
