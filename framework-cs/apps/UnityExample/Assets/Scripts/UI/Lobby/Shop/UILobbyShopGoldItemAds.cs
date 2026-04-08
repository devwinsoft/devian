using UnityEngine;
using UnityEngine.UI;
using Devian;
using Devian.Domain.Common;
using Devian.Domain.Game;
using TMPro;

public class UILobbyShopGoldItemAds : UIBaseFrame
{
    public SHOP_ITEM_GOLD_ID shopItemId;
    
    public TextMeshProUGUI itemNameText;
    public TextMeshProUGUI itemRemainText;
    public Image dim;
    public UI_POPUP_FRAME_ID adsPopupId;

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
        refresh();
    }

    public void OnClick()
    {
        if (product == null)
        {
            return;
        }
        
        UIPopupManager.Instance.Show(adsPopupId, this.shopItemId, (reason) =>
        {
            refresh();
        });
    }

    void refresh()
    {
        if (product == null) return;
        itemRemainText.text = $"{product.RemainCount}/{product.max_count}";
        dim.enabled = product?.RemainCount > 0;
    }

}
