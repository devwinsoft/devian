using Devian;
using UnityEngine;
using System.Threading;
using System.Threading.Tasks;
using Devian;
using Devian.Domain.Game;

public class UILobbyShopPagePanel : UIBasePageMain<UILobbyPageCanvas>
{
    protected override void onInit(UILobbyPageCanvas pageCanvas)
    {
    }
    
    public void OnClick_Purchase_NoAds()
    {
        UnityTaskRunner.Run(OnClickPurchaseNoAdsAsync, $"{nameof(UILobbyPageCanvas)}.{nameof(OnClick_Purchase_NoAds)}");
    }

    public void OnClick_Purchase_Pass()
    {
        UnityTaskRunner.Run(OnClickPurchasePassAsync, $"{nameof(UILobbyPageCanvas)}.{nameof(OnClick_Purchase_Pass)}");
    }

    public void OnClick_Purchase_Chest()
    {
        UnityTaskRunner.Run(OnClickPurchaseChestAsync, $"{nameof(UILobbyPageCanvas)}.{nameof(OnClick_Purchase_Chest)}");
    }
    
    private async Task OnClickPurchaseNoAdsAsync()
    {
        var purchase = await PurchaseManager.Instance.PurchaseAsync(
            "purchase_noads_month",
            CancellationToken.None);
        if (purchase.IsSuccess)
        {
            Debug.Log(purchase.Value.ResultStatus);
        }
        else
        {
            Debug.Log($"{purchase.Error.Code}: {purchase.Error.Message}");
        }
        foreach (var key in InventoryManager.Instance.Storage.Rentals.Keys)
        {
            Debug.LogWarning($"Rentals: {key}");
        }
    }
    
    private async Task OnClickPurchasePassAsync()
    {
        var result = await PurchaseManager.Instance.PurchaseAsync(
            "purchase_pass_001",
            CancellationToken.None);
        if (result.IsSuccess)
        {
            Debug.Log(result.Value.ResultStatus);
            foreach (var reward in result.Value.AppliedRewards)
            {
                Debug.Log($"{reward.Type}, {reward.Id}, {reward.Amount}");
            }
        }
        else
        {
            Debug.Log($"{result.Error.Code}: {result.Error.Message}");
        }
    }


    private async Task OnClickPurchaseChestAsync()
    {
        var result = await ShopManager.Instance.BuyAsync("shop_jewel_1000");
        if (result.IsSuccess)
        {
            foreach (var reward in result.Value)
            {
                Debug.Log($"{reward.Type}, {reward.Id}, {reward.Amount}");
            }
        }
        else
        {
            Debug.Log($"{result.Error.Code}: {result.Error.Message}");
        }
    }
}
