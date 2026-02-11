using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Devian;
using Devian.Protocol.Game;

public class TestUICanvas : UICanvas<TestUICanvas>
{
    protected override void onAwake()
    {
    }

    public async void OnClick_SignIn()
    {
        var init = await FirebaseManager.Instance.InitializeAsync(CancellationToken.None);
        if (!init.IsSuccess)
        {
            Debug.LogError(init.Error.Message);
            return;
        }

        var r = await FirebaseManager.Instance.SignInAnonymouslyAsync(CancellationToken.None);
        if (!r.IsSuccess)
        {
            Debug.LogError(r.Error.Message);
            return;
        }
        Debug.Log(r.Value);
    }

    public async void OnClick_CloudLoad()
    {
        var r = CloudSaveManager.Instance.LoadPayloadAsync("main", CancellationToken.None);
    }
    
    public async void OnClick_CloudSave()
    {
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
