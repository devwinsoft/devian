using UnityEngine;
using Devian;
using Devian.Protocol.Game;

public class TestUICanvas : UICanvas<TestUICanvas>
{
    protected override void onAwake()
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
