using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;
using Devian;
using Devian.Domain.Game;
using TMPro;

public class UILobbyShopDailyItemRotation : UILobbyShopDailyItemBase
{
    public TextMeshProUGUI itemAmountText;
    public TextMeshProUGUI itemPriceText;
    public Image itemSaleFrame;
    public TextMeshProUGUI itemSaleText;
    public int index;

    ShopProductDaily shopDailyProduct;
    int remainCount = 0;

    protected override void onInit()
    {
        base.onInit();

        var rotatingIndex = 0;
        var dailyProducts = ShopManager.Instance.GetCatalog(SHOP_CATALOG_TYPE.DAILY)?.GetProducts();
        for (var i = 0; dailyProducts != null && i < dailyProducts.Count; i++)
        {
            if (dailyProducts[i] is not ShopProductDaily product)
                continue;

            var table = product.Table;
            if (table == null
                || table.currency_type == CURRENCY_TYPE.FREE
                || table.currency_type == CURRENCY_TYPE.ADS)
                continue;

            if (rotatingIndex == index)
            {
                shopDailyProduct = product;
                break;
            }

            rotatingIndex++;
        }

        if (shopDailyProduct == null)
        {
            itemAmountText.text = string.Empty;
            itemNameText.text = string.Empty;
            itemPriceText.text = string.Empty;
        }
        else
        {
            itemAmountText.text = $"x{shopDailyProduct.amount}";
            itemNameText.text = shopDailyProduct.Table.name_id;
            itemPriceText.text = shopDailyProduct.Price.ToString();
        }

        refresh();
    }

    public void OnClick()
    {
        UnityTaskRunner.Run(_buyAsync(), "UILobbyShopDailyItem.OnClick");
    }

    async Task _buyAsync()
    {
        if (shopDailyProduct == null)
            return;

        Debug.Log(shopDailyProduct.shop_id);
        var result = await ShopManager.Instance.BuyAsync(shopDailyProduct.shop_id);
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
        if (shopDailyProduct == null)
        {
            remainCount = 0;
            itemSaleFrame.gameObject.SetActive(false);
            itemSaleText.text = string.Empty;
            dim.gameObject.SetActive(true);
            return;
        }

        remainCount = shopDailyProduct.RemainCount;
        switch (shopDailyProduct.DiscountType)
        {
            case SHOP_DISCOUNT_TYPE.PER10:
                itemSaleFrame.gameObject.SetActive(true);
                itemSaleText.text = "10%";
                break;
            case SHOP_DISCOUNT_TYPE.PER20:
                itemSaleFrame.gameObject.SetActive(true);
                itemSaleText.text = "20%";
                break;
            case SHOP_DISCOUNT_TYPE.PER30:
                itemSaleFrame.gameObject.SetActive(true);
                itemSaleText.text = "30%";
                break;
            case SHOP_DISCOUNT_TYPE.PER50:
                itemSaleFrame.gameObject.SetActive(true);
                itemSaleText.text = "50%";
                break;
            default:
                itemSaleFrame.gameObject.SetActive(false);
                itemSaleText.text = string.Empty;
                break;
        }
        dim.gameObject.SetActive(remainCount <= 0);
    }
}
