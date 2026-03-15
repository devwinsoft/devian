using Devian.Domain.Game;

namespace Devian
{
    public sealed class ShopProductNone : ShopProductBase
    {
        public ShopProductNone(
            string shopId,
            string nameId,
            SHOP_CATALOG_TYPE catalogType,
            int maxCount = -1,
            SHOP_DISCOUNT_TYPE discountType = SHOP_DISCOUNT_TYPE.NONE)
            : base(shopId, nameId, catalogType, SHOP_PRODUCT_TYPE.NONE, maxCount, discountType)
        {
        }
    }
}
