using Devian.Domain.Game;

namespace Devian
{
    public sealed class ShopProductChest : ShopProductBase
    {
        readonly int _priceWithoutDiscount;

        public ShopProductChest(
            string shopId,
            string nameId,
            SHOP_CATALOG_TYPE catalogType,
            SHOP_PRODUCT_CHEST_TYPE chestType,
            CURRENCY_TYPE currencyType,
            int price,
            int amount,
            int maxCount = -1,
            SHOP_DISCOUNT_TYPE discountType = SHOP_DISCOUNT_TYPE.NONE)
            : base(
                shopId,
                nameId,
                catalogType,
                toProductType(chestType, currencyType),
                maxCount,
                discountType)
        {
            ChestType = normalizeChestType(chestType);
            CurrencyType = currencyType;
            _priceWithoutDiscount = price;
            Amount = amount < 1 ? 1 : amount;
        }

        public SHOP_PRODUCT_CHEST_TYPE ChestType { get; }
        public CURRENCY_TYPE CurrencyType { get; }
        public override int PriceWithoutDiscount => _priceWithoutDiscount;
        public int Amount { get; }

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
