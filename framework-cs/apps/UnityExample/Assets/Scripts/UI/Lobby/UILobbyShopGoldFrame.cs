using UnityEngine;
using UnityEngine.UI;
using Devian;
using Devian.Domain.Common;
using Devian.Domain.Game;
using TMPro;

public class UILobbyShopGoldFrame : UIBaseFrame
{
    public SHOP_ITEM_GOLD_ID shopItemId;
    
    public TextMeshProUGUI itemNameText;
    public TextMeshProUGUI itemPriceText;
    public UI_POPUP_FRAME_ID adsPopupId;

    public void OnClick()
    {
        SHOP_ITEM_GOLD table = TB_SHOP_ITEM_GOLD.Get(shopItemId);
        if (table == null)
        {
            return;
        }

        switch (table.currency_type)
        {
            case CURRENCY_TYPE.ADS:
                UIPopupManager.Instance.Show(adsPopupId, this.shopItemId, (reason) =>
                {
                });
                break;
            default:
                break;
        }
        
    }

}
