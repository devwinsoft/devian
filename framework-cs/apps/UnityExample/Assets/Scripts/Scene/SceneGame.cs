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

        UIToastService.Instance.Show("This is a default toast message.",  toastType: ToastType.Error);
        UIToastService.Instance.Show("This is a default toast message.",  toastType: ToastType.Warning);
        UIToastService.Instance.Show("This is a default toast message.");

        UIPopupManager.Instance.Show(
            "ui_popup_confirm",
            onClosed: reason =>
            {
                Debug.Log($"[SceneGame] Popup closed: reason={reason}");
            });
    }
}
