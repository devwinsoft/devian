using Devian.Domain.Game;

namespace Devian
{
    public sealed class ShopProductCurrency : ShopRewardProductBase
    {
        public ShopProductCurrency(
            string shopId,
            string nameId,
            SHOP_CATALOG_TYPE catalogType,
            CURRENCY_TYPE currencyType,
            int price,
            string rewardGroupId,
            int amount,
            int maxCount = -1,
            SHOP_DISCOUNT_TYPE discountType = SHOP_DISCOUNT_TYPE.NONE)
            : base(
                shopId,
                nameId,
                catalogType,
                SHOP_PRODUCT_TYPE.CURRENCY,
                currencyType,
                price,
                rewardGroupId,
                amount,
                maxCount,
                discountType)
        {
        }
    }
}
