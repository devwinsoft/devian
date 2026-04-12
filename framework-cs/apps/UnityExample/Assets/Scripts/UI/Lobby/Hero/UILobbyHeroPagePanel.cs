using UnityEngine;
using Devian;
using Devian.Domain.Common;
using Devian.Domain.Game;

public class UILobbyHeroPagePanel : UIBasePageMain<UILobbyPageCanvas>
{
    public GameObject[] scrolls;
    
    protected override void onInit(UILobbyPageCanvas canvas)
    {
    }

    public void OnClick_Button(int index)
    {
        for (int i = 0; i < scrolls.Length; i++)
        {
            scrolls[i].SetActive(i == index);
        }
    }
}
