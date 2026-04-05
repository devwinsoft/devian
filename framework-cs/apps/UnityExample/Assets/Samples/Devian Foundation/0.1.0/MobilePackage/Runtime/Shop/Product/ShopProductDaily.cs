using Devian.Domain.Game;

namespace Devian
{
    public sealed class ShopProductDaily : ShopRewardProductBase<SHOP_ITEM_DAILY>
    {
        public ShopProductDaily(
            SHOP_ITEM_DAILY table,
            SHOP_DISCOUNT_TYPE discountType,
            int amount = 1)
            : base(
                table,
                table?.shop_item_id,
                table?.name_id,
                SHOP_CATALOG_TYPE.DAILY,
                toProductType(table != null ? table.currency_type : default),
                table != null ? table.currency_type : default,
                table?.price ?? 0,
                table?.reward_group_id,
                amount,
                table?.max_count ?? -1,
                discountType)
        {
        }
        
        protected override int PriceMultiplier => amount;

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
