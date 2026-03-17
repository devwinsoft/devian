using Devian.Domain.Game;

namespace Devian
{
    public sealed class ShopProductPurchase : ShopProductBase
    {
        public ShopProductPurchase(
            string shopId,
            string nameId,
            SHOP_CATALOG_TYPE catalogType,
            string internalProductId,
            string seasonId,
            int maxCount = -1)
            : base(shopId, nameId, catalogType, SHOP_PRODUCT_TYPE.PURCHASE, maxCount, SHOP_DISCOUNT_TYPE.NONE)
        {
            InternalProductId = internalProductId ?? string.Empty;
            SeasonId = seasonId ?? string.Empty;
        }

        public string InternalProductId { get; }
        public string SeasonId { get; }
    }
}
