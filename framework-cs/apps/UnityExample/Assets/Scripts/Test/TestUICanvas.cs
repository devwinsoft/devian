using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Devian;
using Devian.Protocol.Game;

public class TestUICanvas : UICanvas<TestUICanvas>
{
    public GameObject buttonApple;
    public GameObject buttonGoogle;
    public GameObject buttonGuest;
    
    protected override void onAwake()
    {
    }

    public async void OnClick_SignIn_Apple()
    {
        var login = await LoginManager.Instance.LoginAsync(LoginType.AppleLogin, CancellationToken.None);
        Debug.Log(login.Value);
    }
    
    public async void OnClick_SignIn_Google()
    {
        var login = await LoginManager.Instance.LoginAsync(LoginType.GoogleLogin, CancellationToken.None);
        Debug.Log(login.Value);
    }

    public async void OnClick_SignIn_Guest()
    {
        var init = await FirebaseManager.Instance.InitializeAsync(CancellationToken.None);
        if (!init.IsSuccess)
        {
            Debug.LogError(init.Error.Message);
            return;
        }
        Debug.Log(init.Value);

        var login = await LoginManager.Instance.LoginAsync(LoginType.GuestLogin, CancellationToken.None);
        Debug.Log(login.Value);
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
