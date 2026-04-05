using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;
using Devian;
using Devian.Domain.Game;
using TMPro;

public class UILobbyShopDailyItemFreeAds : UILobbyShopDailyItemBase
{
    public SHOP_ITEM_DAILY_ID shopItemAdsId;
    public SHOP_ITEM_DAILY_ID shopItemFreeId;

    string shopItemId = null;
    int remainFreeCount = 0;
    int remainAdsCount = 0;
    
    protected override void onInit()
    {
        base.onInit();

        refresh();
    }
    
    public void OnClick()
    {
        refresh();
        if (!string.IsNullOrEmpty(shopItemId))
        {
            UnityTaskRunner.Run(_buyAsync(), "UILobbyShopDailyItemFreeAds.OnClick");
        }
    }

    async Task _buyAsync()
    {
        var result = await ShopManager.Instance.BuyAsync(shopItemId);
        if (result.IsSuccess)
        {
            foreach (var reward in result.Value)
            {
                UIToastService.Instance.Show(reward.Id);
            }
        }
    }

    void refresh()
    {
        remainFreeCount = ShopManager.Instance.Storage.TryGetDailyCatalogProduct(shopItemFreeId, out var stateFree)
            ? stateFree.remainCount : 0;
        remainAdsCount = ShopManager.Instance.Storage.TryGetDailyCatalogProduct(shopItemAdsId, out var stateAds)
            ? stateAds.remainCount : 0;
        Debug.Log($"shop_item_id={shopItemFreeId.Value}, remainFreeCount={remainFreeCount}, remainAdsCount={remainAdsCount}");

        if (remainFreeCount > 0)
        {
            shopItemId = shopItemFreeId;
            dim.gameObject.SetActive(false);
        }
        else if (remainAdsCount > 0)
        {
            shopItemId = shopItemAdsId;
            dim.gameObject.SetActive(false);
        }
        else
        {
            shopItemId = string.Empty;
            dim.gameObject.SetActive(true);
        }
    }
}
