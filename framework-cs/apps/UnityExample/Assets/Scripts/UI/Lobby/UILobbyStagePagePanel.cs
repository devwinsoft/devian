using UnityEngine;
using Devian;
using Devian.Domain.Common;
using Devian.Domain.Game;

public class UILobbyStagePagePanel : UIBasePageMain<UILobbyPageCanvas>
{
    protected override void onInit(UILobbyPageCanvas pageCanvas)
    {
    }

    public void OnClick_PlayButton()
    {
        UnityTaskRunner.Run(SceneTransManager.Instance.LoadSceneAsync("SceneGame"), "UILobbyStagePanel.OnPlayButtonClick");
    }
}
