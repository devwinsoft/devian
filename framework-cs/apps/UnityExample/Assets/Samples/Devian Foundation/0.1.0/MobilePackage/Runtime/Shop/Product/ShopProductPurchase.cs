using Devian.Domain.Game;

namespace Devian
{
    public sealed class ShopProductPurchase : ShopProductBase
    {
        public ShopProductPurchase(
            string shop_item_id,
            string name_id,
            SHOP_CATALOG_TYPE catalog_type,
            string internal_product_id,
            string season_id,
            int max_count = -1)
            : base(shop_item_id, name_id, catalog_type, SHOP_PRODUCT_TYPE.PURCHASE, max_count, SHOP_DISCOUNT_TYPE.NONE)
        {
            internal_product_id_internal = internal_product_id ?? string.Empty;
            season_id_internal = season_id ?? string.Empty;
        }

        string internal_product_id_internal;
        string season_id_internal;

        public string internal_product_id => internal_product_id_internal;
        public string season_id => season_id_internal;
    }
}
