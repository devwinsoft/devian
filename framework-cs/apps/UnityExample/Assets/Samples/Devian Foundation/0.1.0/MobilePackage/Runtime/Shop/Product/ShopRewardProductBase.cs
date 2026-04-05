using Devian.Domain.Game;

namespace Devian
{
    public abstract class ShopRewardProductBase : ShopLimitedProductBase
    {
        readonly int _unitPriceWithoutDiscount;

        protected ShopRewardProductBase(
            string shop_id,
            string name_id,
            SHOP_CATALOG_TYPE catalog_type,
            SHOP_PRODUCT_TYPE productType,
            CURRENCY_TYPE currency_type,
            int price,
            string reward_group_id,
            int amount,
            int max_count = -1,
            SHOP_DISCOUNT_TYPE discountType = SHOP_DISCOUNT_TYPE.NONE)
            : base(shop_id, name_id, catalog_type, productType, max_count, discountType)
        {
            currency_type_internal = currency_type;
            _unitPriceWithoutDiscount = price;
            reward_group_id_internal = reward_group_id ?? string.Empty;
            setAmount(amount);
        }

        CURRENCY_TYPE currency_type_internal;
        string reward_group_id_internal;
        int amount_internal;

        public CURRENCY_TYPE currency_type => currency_type_internal;
        public override int PriceWithoutDiscount => calculateTotalPrice(_unitPriceWithoutDiscount, PriceMultiplier);
        public string reward_group_id => reward_group_id_internal;
        public int amount => amount_internal;
        protected virtual int PriceMultiplier => 1;

        protected void SetAmount(int amount)
        {
            setAmount(amount);
        }

        void setAmount(int amount)
        {
            amount_internal = normalizeAmount(amount);
        }

        static int calculateTotalPrice(int unitPrice, int amountMultiplier)
        {
            if (unitPrice <= 0)
                return unitPrice;

            var normalizedAmount = normalizeAmount(amountMultiplier);
            if (normalizedAmount == 1)
                return unitPrice;

            var totalPrice = (long)unitPrice * normalizedAmount;
            if (totalPrice > int.MaxValue)
                return int.MaxValue;

            return (int)totalPrice;
        }

        static int normalizeAmount(int amount)
        {
            return amount < 1 ? 1 : amount;
        }
    }

    public abstract class ShopRewardProductBase<TRow> : ShopRewardProductBase, IShopTableProduct<TRow> where TRow : class
    {
        protected ShopRewardProductBase(
            TRow table,
            string shop_id,
            string name_id,
            SHOP_CATALOG_TYPE catalog_type,
            SHOP_PRODUCT_TYPE productType,
            CURRENCY_TYPE currency_type,
            int price,
            string reward_group_id,
            int amount,
            int max_count = -1,
            SHOP_DISCOUNT_TYPE discountType = SHOP_DISCOUNT_TYPE.NONE)
            : base(
                shop_id,
                name_id,
                catalog_type,
                productType,
                currency_type,
                price,
                reward_group_id,
                amount,
                max_count,
                discountType)
        {
            Table = table;
        }

        public TRow Table { get; }
        object IShopTableProduct.TableObject => Table;
    }
}
