using Devian.Domain.Game;

namespace Devian
{
    public abstract class ShopProductBase
    {
        protected ShopProductBase(
            string shop_item_id,
            string name_id,
            SHOP_CATALOG_TYPE catalog_type,
            SHOP_PRODUCT_TYPE productType,
            int max_count = -1,
            SHOP_DISCOUNT_TYPE discountType = SHOP_DISCOUNT_TYPE.NONE)
        {
            shop_item_id_internal = normalize(shop_item_id);
            name_id_internal = normalize(name_id);
            catalog_type_internal = catalog_type;
            ProductType = productType;
            max_count_internal = normalizeMaxCount(max_count);
            RemainCount = max_count_internal;
            DiscountType = normalizeDiscountType(discountType);
        }

        string shop_item_id_internal;
        string name_id_internal;
        SHOP_CATALOG_TYPE catalog_type_internal;
        int max_count_internal;

        public string shop_item_id => shop_item_id_internal;
        public string name_id => name_id_internal;
        public SHOP_CATALOG_TYPE catalog_type => catalog_type_internal;
        public SHOP_PRODUCT_TYPE ProductType { get; }
        public SHOP_DISCOUNT_TYPE DiscountType { get; }
        public virtual int PriceWithoutDiscount => 0;
        public int Price => applyDiscount(PriceWithoutDiscount, DiscountType);
        public int max_count => max_count_internal;
        public int RemainCount { get; private set; }

        public bool HasPurchaseLimit => max_count_internal >= 0;

        public void SetRemainCount(int remainCount)
        {
            RemainCount = sanitizeRemainCount(remainCount, max_count_internal);
        }

        public bool TryConsumeOne()
        {
            if (!HasPurchaseLimit)
                return true;

            if (RemainCount <= 0)
                return false;

            RemainCount -= 1;
            return true;
        }

        public void ResetRemainCount()
        {
            RemainCount = max_count_internal;
        }

        static string normalize(string value)
        {
            return value ?? string.Empty;
        }

        static int normalizeMaxCount(int maxCount)
        {
            return maxCount < -1 ? -1 : maxCount;
        }

        static int sanitizeRemainCount(int remainCount, int maxCount)
        {
            if (maxCount < 0)
                return -1;

            if (remainCount < 0)
                return 0;

            return remainCount > maxCount ? maxCount : remainCount;
        }

        static int applyDiscount(int price, SHOP_DISCOUNT_TYPE discountType)
        {
            if (price <= 0)
                return price;

            var discountPercent = getDiscountPercent(discountType);
            if (discountPercent <= 0)
                return price;

            var discounted = (long)price * (100 - discountPercent);
            if (discounted <= 0L)
                return 0;

            var discountedPrice = discounted / 100L;
            if (discountedPrice <= 0L)
                return 0;

            if (discountedPrice > int.MaxValue)
                return int.MaxValue;

            return (int)discountedPrice;
        }

        static int getDiscountPercent(SHOP_DISCOUNT_TYPE discountType)
        {
            switch (discountType)
            {
                case SHOP_DISCOUNT_TYPE.PER10:
                    return 10;
                case SHOP_DISCOUNT_TYPE.PER20:
                    return 20;
                case SHOP_DISCOUNT_TYPE.PER30:
                    return 30;
                case SHOP_DISCOUNT_TYPE.PER50:
                    return 50;
                default:
                    return 0;
            }
        }

        static SHOP_DISCOUNT_TYPE normalizeDiscountType(SHOP_DISCOUNT_TYPE discountType)
        {
            switch (discountType)
            {
                case SHOP_DISCOUNT_TYPE.PER10:
                case SHOP_DISCOUNT_TYPE.PER20:
                case SHOP_DISCOUNT_TYPE.PER30:
                case SHOP_DISCOUNT_TYPE.PER50:
                    return discountType;
                default:
                    return SHOP_DISCOUNT_TYPE.NONE;
            }
        }
    }
}
