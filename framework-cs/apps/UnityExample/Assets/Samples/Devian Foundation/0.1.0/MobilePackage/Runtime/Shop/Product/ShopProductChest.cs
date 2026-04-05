using Devian.Domain.Game;

namespace Devian
{
    public sealed class ShopProductChest : ShopLimitedProductBase<SHOP_ITEM_CHEST>
    {
        public ShopProductChest(
            SHOP_ITEM_CHEST table,
            SHOP_DISCOUNT_TYPE discountType = SHOP_DISCOUNT_TYPE.NONE)
            : base(
                table,
                table?.shop_item_id,
                table?.name_id,
                SHOP_CATALOG_TYPE.CHEST,
                toProductType(table?.chest_type ?? SHOP_PRODUCT_CHEST_TYPE.NONE, table != null ? table.currency_type : default),
                table?.max_count ?? -1,
                discountType)
        {
        }

        public SHOP_PRODUCT_CHEST_TYPE chest_type => normalizeChestType(Table?.chest_type ?? SHOP_PRODUCT_CHEST_TYPE.NONE);
        public CURRENCY_TYPE currency_type => Table != null ? Table.currency_type : default;
        public override int PriceWithoutDiscount => Table != null ? Table.price : 0;
        public int amount => Table != null && Table.amount > 0 ? Table.amount : 1;

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
