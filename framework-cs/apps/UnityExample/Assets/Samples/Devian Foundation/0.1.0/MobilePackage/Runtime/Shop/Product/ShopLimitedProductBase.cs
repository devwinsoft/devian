using Devian.Domain.Game;

namespace Devian
{
    public abstract class ShopLimitedProductBase : ShopProductBase
    {
        protected ShopLimitedProductBase(
            string shop_id,
            string name_id,
            SHOP_CATALOG_TYPE catalog_type,
            SHOP_PRODUCT_TYPE productType,
            int max_count = -1,
            SHOP_DISCOUNT_TYPE discountType = SHOP_DISCOUNT_TYPE.NONE)
            : base(shop_id, name_id, catalog_type, productType, discountType)
        {
            max_count_internal = normalizeMaxCount(max_count);
            RemainCount = max_count_internal;
        }

        readonly int max_count_internal;

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
    }

    public abstract class ShopLimitedProductBase<TRow> : ShopLimitedProductBase, IShopTableProduct<TRow> where TRow : class
    {
        protected ShopLimitedProductBase(
            TRow table,
            string shop_id,
            string name_id,
            SHOP_CATALOG_TYPE catalog_type,
            SHOP_PRODUCT_TYPE productType,
            int max_count = -1,
            SHOP_DISCOUNT_TYPE discountType = SHOP_DISCOUNT_TYPE.NONE)
            : base(shop_id, name_id, catalog_type, productType, max_count, discountType)
        {
            Table = table;
        }

        public TRow Table { get; }
        object IShopTableProduct.TableObject => Table;
    }
}
