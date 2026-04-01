using Devian.Domain.Game;

namespace Devian
{
    public sealed class ShopProductAds : ShopRewardProductBase
    {
        public ShopProductAds(
            string shop_id,
            string name_id,
            SHOP_CATALOG_TYPE catalog_type,
            int price,
            string reward_group_id,
            int amount,
            int max_count = -1,
            SHOP_DISCOUNT_TYPE discountType = SHOP_DISCOUNT_TYPE.NONE)
            : base(
                shop_id,
                name_id,
                catalog_type,
                SHOP_PRODUCT_TYPE.ADS,
                CURRENCY_TYPE.ADS,
                price,
                reward_group_id,
                amount,
                max_count,
                discountType)
        {
        }
    }
}
