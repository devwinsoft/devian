using UnityEngine;
using Devian;

[RequireComponent(typeof(UIComponentMenuBar))]
public class UILobbyMenuBottomPanel : UIBasePanel<UILobbyPageCanvas>
{
    UIComponentMenuBar _menuBar;

    protected override void onAwake()
    {
        _menuBar = GetComponent<UIComponentMenuBar>();
    }

    protected override void onInit(UILobbyPageCanvas pageCanvas)
    {
        _menuBar.OnSelect +=
            (index) =>
            {
                ownerCanvas.ShowPage(index);
            };
    }
}
