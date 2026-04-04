using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Devian;
using Devian.Domain.Common;
using Devian.Domain.Game;

public class UILobbyAdsPopup : UIPopupFrameBase
{
    public SHOP_ITEM_GOLD_ID shopItemId;

    protected override void onBind(object payload)
    {
        shopItemId = payload as SHOP_ITEM_GOLD_ID;
    }

    public void OnClick_Ads()
    {
        UnityTaskRunner.Run(ShowAds(), "UILobbyAdsPopup.OnClick_Ads");
    }

    public void OnClick_Cancel()
    {
        ClosePopup(PopupCloseReason.Cancel);
    }
    
    
    async Task ShowAds()
    {
        var result = await ShopManager.Instance.BuyAsync(shopItemId);
        if (result.IsSuccess)
        {
            foreach (var reward in result.Value)
            {
                Debug.Log(reward.Id);
            }
            ClosePopup(PopupCloseReason.Confirm);
        }
        else
        {
            if (result.IsFailure)
            {
                Debug.Log(result.Error.Code);
                Debug.Log(result.Error.Message);
            }
            ClosePopup(PopupCloseReason.No);
        }
    }
}
