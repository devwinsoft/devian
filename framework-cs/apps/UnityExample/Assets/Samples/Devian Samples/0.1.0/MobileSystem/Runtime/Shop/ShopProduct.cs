using Devian.Domain.Game;

namespace Devian
{
    public abstract class ShopProductBase
    {
        protected ShopProductBase(
            string shopId,
            string nameId,
            SHOP_CATALOG_TYPE catalogType,
            SHOP_PRODUCT_TYPE productType,
            int maxCount = -1,
            SHOP_DISCOUNT_TYPE discountType = SHOP_DISCOUNT_TYPE.NONE)
        {
            ShopId = normalize(shopId);
            NameId = normalize(nameId);
            CatalogType = catalogType;
            ProductType = productType;
            MaxCount = normalizeMaxCount(maxCount);
            RemainCount = MaxCount;
            DiscountType = normalizeDiscountType(discountType);
        }

        public string ShopId { get; }
        public string ProductId => ShopId; // Legacy alias for old callers.
        public string NameId { get; }
        public SHOP_CATALOG_TYPE CatalogType { get; }
        public SHOP_PRODUCT_TYPE ProductType { get; }
        public SHOP_DISCOUNT_TYPE DiscountType { get; }
        public virtual int PriceWithoutDiscount => 0;
        public int Price => applyDiscount(PriceWithoutDiscount, DiscountType);
        public int MaxCount { get; }
        public int RemainCount { get; private set; }

        public bool HasPurchaseLimit => MaxCount >= 0;

        public void SetRemainCount(int remainCount)
        {
            RemainCount = sanitizeRemainCount(remainCount, MaxCount);
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
            RemainCount = MaxCount;
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

    public abstract class ShopRewardProductBase : ShopProductBase
    {
        readonly int _priceWithoutDiscount;

        protected ShopRewardProductBase(
            string shopId,
            string nameId,
            SHOP_CATALOG_TYPE catalogType,
            SHOP_PRODUCT_TYPE productType,
            CURRENCY_TYPE currencyType,
            int price,
            string rewardGroupId,
            int amount,
            int maxCount = -1,
            SHOP_DISCOUNT_TYPE discountType = SHOP_DISCOUNT_TYPE.NONE)
            : base(shopId, nameId, catalogType, productType, maxCount, discountType)
        {
            CurrencyType = currencyType;
            _priceWithoutDiscount = price;
            RewardGroupId = rewardGroupId ?? string.Empty;
            Amount = amount < 1 ? 1 : amount;
        }

        public CURRENCY_TYPE CurrencyType { get; }
        public override int PriceWithoutDiscount => _priceWithoutDiscount;
        public string RewardGroupId { get; }
        public int Amount { get; }
    }

    public sealed class ShopProductNone : ShopProductBase
    {
        public ShopProductNone(
            string shopId,
            string nameId,
            SHOP_CATALOG_TYPE catalogType,
            int maxCount = -1,
            SHOP_DISCOUNT_TYPE discountType = SHOP_DISCOUNT_TYPE.NONE)
            : base(shopId, nameId, catalogType, SHOP_PRODUCT_TYPE.NONE, maxCount, discountType)
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
            int maxCount = -1,
            SHOP_DISCOUNT_TYPE discountType = SHOP_DISCOUNT_TYPE.NONE)
            : base(
                shopId,
                nameId,
                catalogType,
                SHOP_PRODUCT_TYPE.FREE,
                CURRENCY_TYPE.FREE,
                price,
                rewardGroupId,
                amount,
                maxCount,
                discountType)
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
            int maxCount = -1,
            SHOP_DISCOUNT_TYPE discountType = SHOP_DISCOUNT_TYPE.NONE)
            : base(
                shopId,
                nameId,
                catalogType,
                SHOP_PRODUCT_TYPE.ADS,
                CURRENCY_TYPE.ADS,
                price,
                rewardGroupId,
                amount,
                maxCount,
                discountType)
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
            int maxCount = -1,
            SHOP_DISCOUNT_TYPE discountType = SHOP_DISCOUNT_TYPE.NONE)
            : base(
                shopId,
                nameId,
                catalogType,
                SHOP_PRODUCT_TYPE.CURRENCY,
                currencyType,
                price,
                rewardGroupId,
                amount,
                maxCount,
                discountType)
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
            int maxCount = -1)
            : base(shopId, nameId, catalogType, SHOP_PRODUCT_TYPE.PURCHASE, maxCount, SHOP_DISCOUNT_TYPE.NONE)
        {
            InternalProductId = internalProductId ?? string.Empty;
            SeasonId = seasonId ?? string.Empty;
        }

        public string InternalProductId { get; }
        public string SeasonId { get; }
    }

    public static class ShopProductFactory
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

            return createRewardProduct(
                row.ShopId,
                row.NameId,
                SHOP_CATALOG_TYPE.CHEST,
                row.CurrencyType,
                row.Price,
                row.RewardGroupId,
                row.Amount,
                row.MaxCount,
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
