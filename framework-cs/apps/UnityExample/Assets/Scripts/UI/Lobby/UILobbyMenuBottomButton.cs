using UnityEngine;
using UnityEngine.UI;
using Devian;

public class UILobbyMenuBottomButton : UIComponentMenuButton
{
    public Sprite spriteSelected;
    public Sprite spriteDeselected;
    Image _image;

    protected override void onAwake()
    {
        base.onAwake();
        _image = GetComponent<Image>();
    }

    protected override void onSelect()
    {
        _image.sprite = spriteSelected;
    }

    protected override void onDeselect()
    {
        _image.sprite = spriteDeselected;
    }
}
