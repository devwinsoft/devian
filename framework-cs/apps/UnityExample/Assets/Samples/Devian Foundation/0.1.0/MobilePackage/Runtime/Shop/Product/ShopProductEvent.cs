using Devian.Domain.Game;

namespace Devian
{
    public sealed class ShopProductEvent : ShopRewardProductBase<SHOP_ITEM_EVENT>
    {
        public ShopProductEvent(SHOP_ITEM_EVENT table)
            : base(
                table,
                table?.shop_item_id,
                table?.name_id,
                SHOP_CATALOG_TYPE.EVENT,
                toProductType(table != null ? table.currency_type : default),
                table != null ? table.currency_type : default,
                table?.price ?? 0,
                table?.reward_group_id,
                1,
                -1,
                SHOP_DISCOUNT_TYPE.NONE)
        {
        }

        static SHOP_PRODUCT_TYPE toProductType(CURRENCY_TYPE currencyType)
        {
            return currencyType switch
            {
                CURRENCY_TYPE.FREE => SHOP_PRODUCT_TYPE.FREE,
                CURRENCY_TYPE.ADS => SHOP_PRODUCT_TYPE.ADS,
                _ => SHOP_PRODUCT_TYPE.CURRENCY,
            };
        }
    }
}
