using UnityEngine.Scripting;

namespace Devian
{
    [Preserve]
    public sealed class PurchaseStoreApple : IPurchaseStore
    {
        public string StoreKey => "apple";

        public string BuildVerifyPayload(string receipt)
        {
            return receipt;
        }
    }
}
