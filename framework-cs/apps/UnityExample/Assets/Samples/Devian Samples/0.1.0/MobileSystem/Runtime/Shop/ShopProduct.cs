using System;
using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class ShopCatalog
    {
        static readonly IReadOnlyList<ShopProductBase> EmptyProducts = Array.Empty<ShopProductBase>();

        public SHOP_CATALOG_TYPE CatalogType { get; }
        public IReadOnlyList<ShopProductBase> Products { get; }

        public ShopCatalog(SHOP_CATALOG_TYPE catalogType, IReadOnlyList<ShopProductBase> products)
        {
            CatalogType = catalogType;
            Products = products ?? EmptyProducts;
        }

        public static ShopCatalog Empty(SHOP_CATALOG_TYPE catalogType)
        {
            return new ShopCatalog(catalogType, EmptyProducts);
        }
    }

    public abstract class ShopProductBase
    {
        protected ShopProductBase(
            string shopId,
            string nameId,
            SHOP_CATALOG_TYPE catalogType,
            SHOP_PRODUCT_TYPE productType,
            int maxCount)
        {
            ShopId = normalize(shopId);
            NameId = normalize(nameId);
            CatalogType = catalogType;
            ProductType = productType;
            MaxCount = maxCount;
        }

        public string ShopId { get; }
        public string ProductId => ShopId; // Legacy alias for old callers.
        public string NameId { get; }
        public SHOP_CATALOG_TYPE CatalogType { get; }
        public SHOP_PRODUCT_TYPE ProductType { get; }
        public int MaxCount { get; }

        public bool HasPurchaseLimit => MaxCount >= 0;

        static string normalize(string value)
        {
            return value ?? string.Empty;
        }
    }

    public abstract class ShopRewardProductBase : ShopProductBase
    {
        protected ShopRewardProductBase(
            string shopId,
            string nameId,
            SHOP_CATALOG_TYPE catalogType,
            SHOP_PRODUCT_TYPE productType,
            CURRENCY_TYPE currencyType,
            int price,
            string rewardGroupId,
            int amount,
            int maxCount)
            : base(shopId, nameId, catalogType, productType, maxCount)
        {
            CurrencyType = currencyType;
            Price = price;
            RewardGroupId = rewardGroupId ?? string.Empty;
            Amount = amount < 1 ? 1 : amount;
        }

        public CURRENCY_TYPE CurrencyType { get; }
        public int Price { get; }
        public string RewardGroupId { get; }
        public int Amount { get; }
    }

    public sealed class ShopProductNone : ShopProductBase
    {
        public ShopProductNone(string shopId, string nameId, SHOP_CATALOG_TYPE catalogType, int maxCount)
            : base(shopId, nameId, catalogType, SHOP_PRODUCT_TYPE.NONE, maxCount)
        {
        }
    }

    public sealed class ShopProductFree : ShopRewardProductBase
    {
        public ShopProductFree(
            string shopId,
            string nameId,
            SHOP_CATALOG_TYPE catalogType,
            int price,
            string rewardGroupId,
            int amount,
            int maxCount)
            : base(
                shopId,
                nameId,
                catalogType,
                SHOP_PRODUCT_TYPE.FREE,
                CURRENCY_TYPE.FREE,
                price,
                rewardGroupId,
                amount,
                maxCount)
        {
        }
    }

    public sealed class ShopProductAds : ShopRewardProductBase
    {
        public ShopProductAds(
            string shopId,
            string nameId,
            SHOP_CATALOG_TYPE catalogType,
            int price,
            string rewardGroupId,
            int amount,
            int maxCount)
            : base(
                shopId,
                nameId,
                catalogType,
                SHOP_PRODUCT_TYPE.ADS,
                CURRENCY_TYPE.ADS,
                price,
                rewardGroupId,
                amount,
                maxCount)
        {
        }
    }

    public sealed class ShopProductCurrency : ShopRewardProductBase
    {
        public ShopProductCurrency(
            string shopId,
            string nameId,
            SHOP_CATALOG_TYPE catalogType,
            CURRENCY_TYPE currencyType,
            int price,
            string rewardGroupId,
            int amount,
            int maxCount)
            : base(
                shopId,
                nameId,
                catalogType,
                SHOP_PRODUCT_TYPE.CURRENCY,
                currencyType,
                price,
                rewardGroupId,
                amount,
                maxCount)
        {
        }
    }

    public sealed class ShopProductPurchase : ShopProductBase
    {
        public ShopProductPurchase(
            string shopId,
            string nameId,
            SHOP_CATALOG_TYPE catalogType,
            string internalProductId,
            string seasonId,
            int maxCount)
            : base(shopId, nameId, catalogType, SHOP_PRODUCT_TYPE.PURCHASE, maxCount)
        {
            InternalProductId = internalProductId ?? string.Empty;
            SeasonId = seasonId ?? string.Empty;
        }

        public string InternalProductId { get; }
        public string SeasonId { get; }
    }

    public static class ShopProductFactory
    {
        public static ShopCatalog BuildCatalog(SHOP_CATALOG_TYPE catalogType)
        {
            var products = BuildCatalogProducts(catalogType);
            return new ShopCatalog(catalogType, products);
        }

        public static IReadOnlyList<ShopProductBase> BuildCatalogProducts(SHOP_CATALOG_TYPE catalogType)
        {
            switch (catalogType)
            {
                case SHOP_CATALOG_TYPE.DAILY:
                    return buildDailyProducts();
                case SHOP_CATALOG_TYPE.CHEST:
                    return buildChestProducts();
                case SHOP_CATALOG_TYPE.PURCHASE:
                    return buildPurchaseProducts();
                case SHOP_CATALOG_TYPE.GOLD:
                    return buildGoldProducts();
                default:
                    return Array.Empty<ShopProductBase>();
            }
        }

        public static ShopProductBase Get(string shopId)
        {
            if (string.IsNullOrWhiteSpace(shopId))
                return null;

            var normalizedShopId = shopId.Trim();

            var dailyRow = TB_SHOP_DAILY.Get(normalizedShopId);
            if (dailyRow != null)
                return createDailyProduct(dailyRow);

            var chestRow = TB_SHOP_CHEST.Get(normalizedShopId);
            if (chestRow != null)
                return createChestProduct(chestRow);

            var purchaseRow = TB_SHOP_PURCHASE.Get(normalizedShopId);
            if (purchaseRow != null)
                return createPurchaseProduct(purchaseRow);

            var goldRow = TB_SHOP_GOLD.Get(normalizedShopId);
            return goldRow != null ? createGoldProduct(goldRow) : null;
        }

        public static bool TryGet(string shopId, out ShopProductBase product)
        {
            product = Get(shopId);
            return product != null;
        }

        static IReadOnlyList<ShopProductBase> buildDailyProducts()
        {
            var rows = TB_SHOP_DAILY.GetAll();
            var list = new List<ShopProductBase>(rows.Count);
            for (var i = 0; i < rows.Count; i++)
                list.Add(createDailyProduct(rows[i]));

            return list;
        }

        static IReadOnlyList<ShopProductBase> buildChestProducts()
        {
            var rows = TB_SHOP_CHEST.GetAll();
            var list = new List<ShopProductBase>(rows.Count);
            for (var i = 0; i < rows.Count; i++)
                list.Add(createChestProduct(rows[i]));

            return list;
        }

        static IReadOnlyList<ShopProductBase> buildPurchaseProducts()
        {
            var rows = TB_SHOP_PURCHASE.GetAll();
            var list = new List<ShopProductBase>(rows.Count);
            for (var i = 0; i < rows.Count; i++)
                list.Add(createPurchaseProduct(rows[i]));

            return list;
        }

        static IReadOnlyList<ShopProductBase> buildGoldProducts()
        {
            var rows = TB_SHOP_GOLD.GetAll();
            var list = new List<ShopProductBase>(rows.Count);
            for (var i = 0; i < rows.Count; i++)
                list.Add(createGoldProduct(rows[i]));

            return list;
        }

        static ShopProductBase createDailyProduct(SHOP_DAILY row)
        {
            return createRewardProduct(
                row.ShopId,
                row.NameId,
                SHOP_CATALOG_TYPE.DAILY,
                row.CurrencyType,
                row.Price,
                row.RewardGroupId,
                row.Amount,
                row.MaxCount);
        }

        static ShopProductBase createChestProduct(SHOP_CHEST row)
        {
            return createRewardProduct(
                row.ShopId,
                row.NameId,
                SHOP_CATALOG_TYPE.CHEST,
                row.CurrencyType,
                row.Price,
                row.RewardGroupId,
                row.Amount,
                row.MaxCount);
        }

        static ShopProductBase createPurchaseProduct(SHOP_PURCHASE row)
        {
            return new ShopProductPurchase(
                row.ShopId,
                row.NameId,
                SHOP_CATALOG_TYPE.PURCHASE,
                row.InternalProductId,
                row.SeasonId,
                -1);
        }

        static ShopProductBase createGoldProduct(SHOP_GOLD row)
        {
            return createRewardProduct(
                row.ShopId,
                row.NameId,
                SHOP_CATALOG_TYPE.GOLD,
                row.CurrencyType,
                row.Price,
                row.RewardGroupId,
                1,
                row.MaxCount);
        }

        static ShopProductBase createRewardProduct(
            string shopId,
            string nameId,
            SHOP_CATALOG_TYPE catalogType,
            CURRENCY_TYPE currencyType,
            int price,
            string rewardGroupId,
            int amount,
            int maxCount)
        {
            switch (currencyType)
            {
                case CURRENCY_TYPE.FREE:
                    return new ShopProductFree(shopId, nameId, catalogType, price, rewardGroupId, amount, maxCount);
                case CURRENCY_TYPE.ADS:
                    return new ShopProductAds(shopId, nameId, catalogType, price, rewardGroupId, amount, maxCount);
                default:
                    return new ShopProductCurrency(
                        shopId,
                        nameId,
                        catalogType,
                        currencyType,
                        price,
                        rewardGroupId,
                        amount,
                        maxCount);
            }
        }
    }
}
