using Devian.Domain.Game;

namespace Devian
{
    public sealed class ShopProductChest : ShopProductBase
    {
        readonly int _priceWithoutDiscount;

        public ShopProductChest(
            string shop_item_id,
            string name_id,
            SHOP_CATALOG_TYPE catalog_type,
            SHOP_PRODUCT_CHEST_TYPE chest_type,
            CURRENCY_TYPE currency_type,
            int price,
            int amount,
            int max_count = -1,
            SHOP_DISCOUNT_TYPE discountType = SHOP_DISCOUNT_TYPE.NONE)
            : base(
                shop_item_id,
                name_id,
                catalog_type,
                toProductType(chest_type, currency_type),
                max_count,
                discountType)
        {
            chest_type_internal = normalizeChestType(chest_type);
            currency_type_internal = currency_type;
            _priceWithoutDiscount = price;
            amount_internal = amount < 1 ? 1 : amount;
        }

        SHOP_PRODUCT_CHEST_TYPE chest_type_internal;
        CURRENCY_TYPE currency_type_internal;
        int amount_internal;

        public SHOP_PRODUCT_CHEST_TYPE chest_type => chest_type_internal;
        public CURRENCY_TYPE currency_type => currency_type_internal;
        public override int PriceWithoutDiscount => _priceWithoutDiscount;
        public int amount => amount_internal;

        static SHOP_PRODUCT_TYPE toProductType(
            SHOP_PRODUCT_CHEST_TYPE chestType,
            CURRENCY_TYPE currencyType)
        {
            return chestType switch
            {
                SHOP_PRODUCT_CHEST_TYPE.ADS => SHOP_PRODUCT_TYPE.ADS,
                SHOP_PRODUCT_CHEST_TYPE.ONE => SHOP_PRODUCT_TYPE.CURRENCY,
                SHOP_PRODUCT_CHEST_TYPE.TEN => SHOP_PRODUCT_TYPE.CURRENCY,
                _ when currencyType == CURRENCY_TYPE.ADS => SHOP_PRODUCT_TYPE.ADS,
                _ => SHOP_PRODUCT_TYPE.NONE,
            };
        }

        static SHOP_PRODUCT_CHEST_TYPE normalizeChestType(SHOP_PRODUCT_CHEST_TYPE chestType)
        {
            return chestType switch
            {
                SHOP_PRODUCT_CHEST_TYPE.ADS => chestType,
                SHOP_PRODUCT_CHEST_TYPE.ONE => chestType,
                SHOP_PRODUCT_CHEST_TYPE.TEN => chestType,
                _ => SHOP_PRODUCT_CHEST_TYPE.NONE,
            };
        }
    }
}
