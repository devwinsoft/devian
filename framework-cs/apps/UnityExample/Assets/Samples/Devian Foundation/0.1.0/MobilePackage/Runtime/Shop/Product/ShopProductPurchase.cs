using Devian.Domain.Game;

namespace Devian
{
    public sealed class ShopProductPurchase : ShopProductBase<SHOP_ITEM_PURCHASE>
    {
        public ShopProductPurchase(SHOP_ITEM_PURCHASE table)
            : base(
                table,
                table?.shop_item_id,
                table?.name_id,
                SHOP_CATALOG_TYPE.PURCHASE,
                SHOP_PRODUCT_TYPE.PURCHASE,
                SHOP_DISCOUNT_TYPE.NONE)
        {
        }

        public string internal_product_id => Table?.internal_product_id ?? string.Empty;
        public string season_id => Table?.season_id ?? string.Empty;
    }
}
