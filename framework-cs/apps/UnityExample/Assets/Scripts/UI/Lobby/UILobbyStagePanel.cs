using UnityEngine;
using Devian;
using Devian.Domain.Common;
using Devian.Domain.Game;

public class UILobbyStagePanel : UIBasePanel<UILobbyCanvas>
{
    protected override void onInit(UILobbyCanvas canvas)
    {
    }

    public void OnClick_PlayButton()
    {
        UnityTaskRunner.Run(SceneTransManager.Instance.LoadSceneAsync("SceneGame"), "UILobbyStagePanel.OnPlayButtonClick");
    }
}
