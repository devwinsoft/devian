using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Devian;
using Devian.Domain.Game;

public class SceneGame : SceneBootstrap
{
    protected override Task onEnter()
    {
        return Task.CompletedTask;
    }

    protected override Task onExit()
    {
        return Task.CompletedTask;
    }
    
    protected override async Task onStart()
    {
        Debug.Log("SceneGame.onStart()...");
        await base.onStart();
        
        await TestApplication.Instance.LoadAsync();
        
        UIGameCanvas.Instance.Init();
        GameMessageManager.Instance.Notify(GAME_MESSAGE_TYPE.ACHIEVE_001, 1);
        
        ToastRequest request = new ToastRequest("Default", "This is a default toast message.");
        UIToastService.Instance.Show(request);
        UIToastService.Instance.Show(request);

        UIPopupManager.Instance.Open<UIPopupFrameConfirm>(
            new ConfirmPopupRequest(
                title: "Popup Sample",
                message: "This popup exercises the typed popup flow."),
            reason =>
            {
                Debug.Log($"[SceneGame] Popup closed: reason={reason}");
            });
    }
}
