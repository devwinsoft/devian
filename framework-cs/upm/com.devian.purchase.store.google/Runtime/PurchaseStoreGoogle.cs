namespace Devian
{
    public sealed class PurchaseStoreGoogle : IPurchaseStore
    {
        public string StoreKey => "google";

        public string BuildVerifyPayload(string receipt)
        {
            return receipt;
        }
    }
}
