using Devian.Domain.Game;

namespace Devian
{
    public sealed class ShopProductNone : ShopProductBase
    {
        public ShopProductNone(
            string shop_item_id,
            string name_id,
            SHOP_CATALOG_TYPE catalog_type,
            int max_count = -1,
            SHOP_DISCOUNT_TYPE discountType = SHOP_DISCOUNT_TYPE.NONE)
            : base(shop_item_id, name_id, catalog_type, SHOP_PRODUCT_TYPE.NONE, max_count, discountType)
        {
        }
    }
}
