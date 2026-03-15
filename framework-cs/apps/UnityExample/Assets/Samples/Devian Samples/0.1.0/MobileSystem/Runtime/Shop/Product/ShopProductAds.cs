using Devian.Domain.Game;

namespace Devian
{
    public sealed class ShopProductAds : ShopRewardProductBase
    {
        public ShopProductAds(
            string shopId,
            string nameId,
            SHOP_CATALOG_TYPE catalogType,
            int price,
            string rewardGroupId,
            int amount,
            int maxCount = -1,
            SHOP_DISCOUNT_TYPE discountType = SHOP_DISCOUNT_TYPE.NONE)
            : base(
                shopId,
                nameId,
                catalogType,
                SHOP_PRODUCT_TYPE.ADS,
                CURRENCY_TYPE.ADS,
                price,
                rewardGroupId,
                amount,
                maxCount,
                discountType)
        {
        }
    }
}
