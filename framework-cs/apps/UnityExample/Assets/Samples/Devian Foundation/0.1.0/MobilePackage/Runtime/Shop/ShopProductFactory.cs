using System;
using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    internal static class ShopProductFactory
    {
        public static ShopProductBase CreateDailyProduct(
            SHOP_ITEM_DAILY row,
            SHOP_DISCOUNT_TYPE discountType,
            int amount = 1)
        {
            return row != null
                ? new ShopProductDaily(row, discountType, amount)
                : null;
        }

        public static ShopProductBase CreateChestProduct(SHOP_ITEM_CHEST row)
        {
            if (row == null)
                return null;

            return new ShopProductChest(row, SHOP_DISCOUNT_TYPE.NONE);
        }

        public static ShopProductBase CreateEventProduct(SHOP_ITEM_EVENT row)
        {
            if (row == null)
                return null;

            return new ShopProductEvent(row);
        }

        public static ShopProductBase CreatePurchaseProduct(SHOP_ITEM_PURCHASE row)
        {
            if (row == null)
                return null;

            return new ShopProductPurchase(row);
        }

        public static ShopProductBase CreateGoldProduct(SHOP_ITEM_GOLD row)
        {
            if (row == null)
                return null;

            return new ShopProductGold(row);
        }

        public static IReadOnlyList<ShopProductBase> BuildProductsFromRows<TRow>(
            IReadOnlyList<TRow> rows,
            Func<TRow, ShopProductBase> createProduct)
        {
            if (rows == null || rows.Count <= 0 || createProduct == null)
                return Array.Empty<ShopProductBase>();

            var products = new List<ShopProductBase>(rows.Count);
            for (var i = 0; i < rows.Count; i++)
            {
                var product = createProduct(rows[i]);
                if (product != null)
                    products.Add(product);
            }

            return products;
        }
    }
}
