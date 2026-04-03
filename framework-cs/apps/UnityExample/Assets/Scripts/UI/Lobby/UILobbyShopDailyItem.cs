using UnityEngine;
using UnityEngine.UI;
using Devian;
using Devian.Domain.Game;
using TMPro;

public class UILobbyShopDailyItem : UIBaseFrame
{
    public int index;
    public TextMeshProUGUI itemNameText;
    public TextMeshProUGUI itemPriceText;
    public Image itemSaleFrame;
    public TextMeshProUGUI itemSaleText;
    public Image dim;

    protected override void onInit()
    {
    }
    
    public void OnClick()
    {
    }
}
