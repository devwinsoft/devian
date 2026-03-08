using System;
using UnityEngine;
using Devian;
using Devian.Protocol.Game;

public class GameNetManager : CompoSingleton<GameNetManager>
{
    public static C2Game.Proxy Proxy => Instance?._networker.Proxy;
    
    private Game2CNetworker _networker = new Game2CNetworker();

    protected override void onDestroy()
    {
        _networker.Disconnect();
    }

    public void SetHandler(Game2C.IHandler handler) => _networker.SetHandler(handler);
    public void Connect(string url) => _networker.Connect(url);
    public void Disconnect() => _networker.Disconnect();
    void Update() => _networker.Tick();
}
