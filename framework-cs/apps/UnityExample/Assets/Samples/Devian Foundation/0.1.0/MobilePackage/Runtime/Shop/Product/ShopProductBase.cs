using Devian.Domain.Game;

namespace Devian
{
    public interface IShopTableProduct
    {
        object TableObject { get; }
    }

    public interface IShopTableProduct<out TRow> : IShopTableProduct where TRow : class
    {
        TRow Table { get; }
    }

    public abstract class ShopProductBase
    {
        protected ShopProductBase(
            string shop_id,
            string name_id,
            SHOP_CATALOG_TYPE catalog_type,
            SHOP_PRODUCT_TYPE productType,
            SHOP_DISCOUNT_TYPE discountType = SHOP_DISCOUNT_TYPE.NONE)
        {
            shop_id_internal = normalize(shop_id);
            name_id_internal = normalize(name_id);
            catalog_type_internal = catalog_type;
            ProductType = productType;
            DiscountType = normalizeDiscountType(discountType);
        }

        string shop_id_internal;
        string name_id_internal;
        SHOP_CATALOG_TYPE catalog_type_internal;

        public string shop_id => shop_id_internal;
        public string ProductId => shop_id_internal; // Legacy alias for old callers.
        public string name_id => name_id_internal;
        public SHOP_CATALOG_TYPE catalog_type => catalog_type_internal;
        public SHOP_PRODUCT_TYPE ProductType { get; }
        public SHOP_DISCOUNT_TYPE DiscountType { get; }
        public virtual int PriceWithoutDiscount => 0;
        public int Price => applyDiscount(PriceWithoutDiscount, DiscountType);

        static string normalize(string value)
        {
            return value ?? string.Empty;
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

    public abstract class ShopProductBase<TRow> : ShopProductBase, IShopTableProduct<TRow> where TRow : class
    {
        protected ShopProductBase(
            TRow table,
            string shop_id,
            string name_id,
            SHOP_CATALOG_TYPE catalog_type,
            SHOP_PRODUCT_TYPE productType,
            SHOP_DISCOUNT_TYPE discountType = SHOP_DISCOUNT_TYPE.NONE)
            : base(shop_id, name_id, catalog_type, productType, discountType)
        {
            Table = table;
        }

        public TRow Table { get; }
        object IShopTableProduct.TableObject => Table;
    }
}
