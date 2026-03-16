using System;
using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    internal static class ShopProductFactory
    {
        public static ShopProductBase CreateDailyProduct(
            SHOP_DAILY row,
            SHOP_DISCOUNT_TYPE discountType)
        {
            if (row == null)
                return null;

            return createRewardProduct(
                row.ShopId,
                row.NameId,
                SHOP_CATALOG_TYPE.DAILY,
                row.CurrencyType,
                row.Price,
                row.RewardGroupId,
                row.Amount,
                row.MaxCount,
                discountType);
        }

        public static ShopProductBase CreateChestProduct(SHOP_CHEST row)
        {
            if (row == null)
                return null;

            return new ShopProductChest(
                row.ShopId,
                row.NameId,
                SHOP_CATALOG_TYPE.CHEST,
                row.ChestType,
                row.CurrencyType,
                row.Price,
                row.Amount,
                row.MaxCount,
                SHOP_DISCOUNT_TYPE.NONE);
        }

        public static ShopProductBase CreateEventProduct(SHOP_EVENT row)
        {
            if (row == null)
                return null;

            return createRewardProduct(
                row.ShopId,
                row.NameId,
                SHOP_CATALOG_TYPE.EVENT,
                row.CurrencyType,
                row.Price,
                row.RewardGroupId,
                1,
                -1,
                SHOP_DISCOUNT_TYPE.NONE);
        }

        public static ShopProductBase CreatePurchaseProduct(SHOP_PURCHASE row)
        {
            if (row == null)
                return null;

            return new ShopProductPurchase(
                row.ShopId,
                row.NameId,
                SHOP_CATALOG_TYPE.PURCHASE,
                row.InternalProductId,
                row.SeasonId,
                -1);
        }

        public static ShopProductBase CreateGoldProduct(SHOP_GOLD row)
        {
            if (row == null)
                return null;

            return createRewardProduct(
                row.ShopId,
                row.NameId,
                SHOP_CATALOG_TYPE.GOLD,
                row.CurrencyType,
                row.Price,
                row.RewardGroupId,
                1,
                row.MaxCount,
                SHOP_DISCOUNT_TYPE.NONE);
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

        static ShopProductBase createRewardProduct(
            string shopId,
            string nameId,
            SHOP_CATALOG_TYPE catalogType,
            CURRENCY_TYPE currencyType,
            int price,
            string rewardGroupId,
            int amount,
            int maxCount,
            SHOP_DISCOUNT_TYPE discountType)
        {
            switch (currencyType)
            {
                case CURRENCY_TYPE.FREE:
                    return new ShopProductFree(shopId, nameId, catalogType, price, rewardGroupId, amount, maxCount, discountType);
                case CURRENCY_TYPE.ADS:
                    return new ShopProductAds(shopId, nameId, catalogType, price, rewardGroupId, amount, maxCount, discountType);
                default:
                    return new ShopProductCurrency(
                        shopId,
                        nameId,
                        catalogType,
                        currencyType,
                        price,
                        rewardGroupId,
                        amount,
                        maxCount,
                        discountType);
            }
        }
    }
}
