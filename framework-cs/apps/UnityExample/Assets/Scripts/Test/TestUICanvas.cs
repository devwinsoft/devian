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
        LoginManager.Instance.Initialize();
    }

    public async void OnClick_SignIn_Apple()
    {
        var login = await LoginManager.Instance.LoginAsync(LoginType.AppleLogin, CancellationToken.None);
        Debug.Log($"SignIn Apple: {login.IsSuccess}");
    }
    
    public async void OnClick_SignIn_Google()
    {
        var login = await LoginManager.Instance.LoginAsync(LoginType.GoogleLogin, CancellationToken.None);
        Debug.Log($"SignIn Google: {login.IsSuccess}");
        if (login.IsSuccess)
        {
            var sync = await SyncDataManager.Instance.SyncAsync(CancellationToken.None);
            Debug.Log($"Sync: {sync.IsSuccess}");
        }
    }

    public async void OnClick_SignIn_Guest()
    {
        var login = await LoginManager.Instance.LoginAsync(LoginType.GuestLogin, CancellationToken.None);
        Debug.Log($"SignIn Guest: {login.IsSuccess} {(login.IsFailure ? login.Error : "")}");
        if (login.IsSuccess)
        {
            var sync = await SyncDataManager.Instance.SyncAsync(CancellationToken.None);
            Debug.Log($"Sync: {sync.IsSuccess}");
        }
    }

    public void OnClick_Logout()
    {
        Debug.Log(Application.persistentDataPath);
        LoginManager.Instance.Logout();
        Debug.Log($"Logout");
    }
    

    public void OnClick_Connect()
    {
        GameNetManager.Instance.Connect("ws://localhost:8080");
    }
    public void OnClick_Echo()
    {
        var msg = new C2Game.Echo();
        msg.Message = "Echo Message";
        GameNetManager.Proxy.SendEcho(msg);
    }
    public void OnClick_DisConnect()
    {
        GameNetManager.Instance.Disconnect();
    }
}
