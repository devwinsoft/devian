using UnityEngine;
using System.Threading;
using System.Threading.Tasks;
using Devian;
using Devian.Domain.Game;
using TMPro;

public class UILobbyShopJewelFrame : UIBaseFrame
{
    public SHOP_ITEM_PURCHASE_ID shopItemId;
    
    public TextMeshProUGUI itemNameText;
    public TextMeshProUGUI itemPriceText;
    
    public void OnClick()
    {
        UnityTaskRunner.Run(BuyAsync(), "UILobbyShopJewelFrame.OnClick");
    }

    async Task BuyAsync()
    {
        var result = await ShopManager.Instance.BuyAsync(shopItemId);
        if (result.IsFailure)
        {
            Debug.LogError(result.Error.Message);
        }
    }
}
