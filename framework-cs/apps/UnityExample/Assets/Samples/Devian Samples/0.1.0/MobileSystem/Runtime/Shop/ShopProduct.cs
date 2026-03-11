using Devian.Domain.Game;

namespace Devian
{
    public sealed class ShopProduct
    {
        public string ProductId { get; }
        public string NameId { get; }
        public string RewardGroupId { get; }
        public CURRENCY_TYPE CurrencyType { get; }
        public int Price { get; }
        public int Amount { get; }
        public int MaxCount { get; }
        public int ResetDays { get; }

        ShopProduct(SHOP_PRODUCT row)
        {
            ProductId = row.ProductId ?? string.Empty;
            NameId = row.NameId ?? string.Empty;
            RewardGroupId = row.RewardGroupId ?? string.Empty;
            CurrencyType = row.CurrencyType;
            Price = row.Price;
            Amount = row.Amount;
            MaxCount = row.MaxCount;
            ResetDays = row.ResetDays;
        }

        public static ShopProduct Get(string productId)
        {
            if (string.IsNullOrWhiteSpace(productId))
                return null;

            var row = TB_SHOP_PRODUCT.Get(productId.Trim());
            return row != null ? new ShopProduct(row) : null;
        }

        public static bool TryGet(string productId, out ShopProduct product)
        {
            product = Get(productId);
            return product != null;
        }
    }
}
