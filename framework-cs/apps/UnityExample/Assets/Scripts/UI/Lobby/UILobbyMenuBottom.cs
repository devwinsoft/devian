using UnityEngine;
using Devian;

public class UILobbyMenuBottom : UIBasePanel<UILobbyPageCanvas>
{
    protected override void onInit(UILobbyPageCanvas pageCanvas)
    {
    }

    public void OnButtonClick(int index)
    {
        ownerCanvas.ShowPage(index);
    }
}
