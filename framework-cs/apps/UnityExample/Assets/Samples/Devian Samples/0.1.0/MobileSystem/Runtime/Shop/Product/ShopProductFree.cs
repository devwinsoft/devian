using Devian.Domain.Game;

namespace Devian
{
    public sealed class ShopProductFree : ShopRewardProductBase
    {
        public ShopProductFree(
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
                SHOP_PRODUCT_TYPE.FREE,
                CURRENCY_TYPE.FREE,
                price,
                rewardGroupId,
                amount,
                maxCount,
                discountType)
        {
        }
    }
}
