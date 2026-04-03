using Devian.Domain.Game;

namespace Devian
{
    public abstract class ShopRewardProductBase : ShopProductBase
    {
        readonly int _priceWithoutDiscount;

        protected ShopRewardProductBase(
            string shop_item_id,
            string name_id,
            SHOP_CATALOG_TYPE catalog_type,
            SHOP_PRODUCT_TYPE productType,
            CURRENCY_TYPE currency_type,
            int price,
            string reward_group_id,
            int amount,
            int max_count = -1,
            SHOP_DISCOUNT_TYPE discountType = SHOP_DISCOUNT_TYPE.NONE)
            : base(shop_item_id, name_id, catalog_type, productType, max_count, discountType)
        {
            currency_type_internal = currency_type;
            _priceWithoutDiscount = price;
            reward_group_id_internal = reward_group_id ?? string.Empty;
            amount_internal = amount < 1 ? 1 : amount;
        }

        CURRENCY_TYPE currency_type_internal;
        string reward_group_id_internal;
        int amount_internal;

        public CURRENCY_TYPE currency_type => currency_type_internal;
        public override int PriceWithoutDiscount => _priceWithoutDiscount;
        public string reward_group_id => reward_group_id_internal;
        public int amount => amount_internal;
    }
}
