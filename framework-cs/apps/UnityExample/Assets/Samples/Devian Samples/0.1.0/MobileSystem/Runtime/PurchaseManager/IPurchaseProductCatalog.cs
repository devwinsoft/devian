using System.Collections.Generic;

namespace Devian
{
    public enum PurchaseProductType
    {
        Consumable = 0,
        NonConsumable = 1,
        Subscription = 2,
    }

    public readonly struct PurchaseCatalogItem
    {
        public readonly string InternalProductId;
        public readonly string StoreSku;
        public readonly PurchaseProductType ProductType;

        public PurchaseCatalogItem(string internalProductId, string storeSku, PurchaseProductType productType)
        {
            InternalProductId = internalProductId;
            StoreSku = storeSku;
            ProductType = productType;
        }
    }

    public interface IPurchaseProductCatalog
    {
        IReadOnlyList<PurchaseCatalogItem> GetActiveProducts();
    }
}
