using Devian.Domain.Game;

namespace Devian
{
    public sealed class ShopProductCurrency : ShopRewardProductBase
    {
        public ShopProductCurrency(
            string shop_item_id,
            string name_id,
            SHOP_CATALOG_TYPE catalog_type,
            CURRENCY_TYPE currency_type,
            int price,
            string reward_group_id,
            int amount,
            int max_count = -1,
            SHOP_DISCOUNT_TYPE discountType = SHOP_DISCOUNT_TYPE.NONE)
            : base(
                shop_item_id,
                name_id,
                catalog_type,
                SHOP_PRODUCT_TYPE.CURRENCY,
                currency_type,
                price,
                reward_group_id,
                amount,
                max_count,
                discountType)
        {
        }
    }
}
