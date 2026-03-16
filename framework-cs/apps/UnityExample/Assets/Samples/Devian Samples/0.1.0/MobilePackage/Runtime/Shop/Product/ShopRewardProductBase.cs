using Devian.Domain.Game;

namespace Devian
{
    public abstract class ShopRewardProductBase : ShopProductBase
    {
        readonly int _priceWithoutDiscount;

        protected ShopRewardProductBase(
            string shopId,
            string nameId,
            SHOP_CATALOG_TYPE catalogType,
            SHOP_PRODUCT_TYPE productType,
            CURRENCY_TYPE currencyType,
            int price,
            string rewardGroupId,
            int amount,
            int maxCount = -1,
            SHOP_DISCOUNT_TYPE discountType = SHOP_DISCOUNT_TYPE.NONE)
            : base(shopId, nameId, catalogType, productType, maxCount, discountType)
        {
            CurrencyType = currencyType;
            _priceWithoutDiscount = price;
            RewardGroupId = rewardGroupId ?? string.Empty;
            Amount = amount < 1 ? 1 : amount;
        }

        public CURRENCY_TYPE CurrencyType { get; }
        public override int PriceWithoutDiscount => _priceWithoutDiscount;
        public string RewardGroupId { get; }
        public int Amount { get; }
    }
}
