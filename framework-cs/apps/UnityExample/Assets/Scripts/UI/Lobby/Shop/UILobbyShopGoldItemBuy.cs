using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;
using Devian;
using Devian.Domain.Common;
using Devian.Domain.Game;
using TMPro;

public class UILobbyShopGoldItemBuy : UIBaseFrame
{
    public SHOP_ITEM_GOLD_ID shopItemId;

    public TextMeshProUGUI itemNameText;
    public TextMeshProUGUI itemPriceText;

    ShopProductGold product = null;

    protected override void onInit()
    {
        var result = ShopManager.Instance.GetGoldProduct(shopItemId);
        if (result.IsFailure)
        {
            Debug.LogError(result.Error.Message);
            return;
        }

        product = result.Value as ShopProductGold;
        itemPriceText.text = product.Price.ToString();
    }

    public void OnClick()
    {
        UnityTaskRunner.Run(_buyAsync(), "UILobbyShopGoldItemBuy.OnClick");
    }

    async Task _buyAsync()
    {
        var result = await ShopManager.Instance.BuyAsync(shopItemId);
        if (result.IsFailure)
        {
            Debug.LogError(result.Error.Message);
            return;
        }

        foreach (var rewardData in result.Value)
        {
            UIToastService.Instance.Show(rewardData.Id.ToString());
        }
    }
}
