using Newtonsoft.Json.Linq;

namespace Devian
{
    internal static class GameStorageJsonCodec
    {
        const int CurrentVersion = 10;

        public static string Serialize(
            InventoryStorage inventory,
            PurchaseStorage purchase,
            AccountStorage account)
        {
            var root = new JObject
            {
                ["version"] = CurrentVersion,
                ["inventory"] = GameStorageJsonCodecInventory.Serialize(inventory),
                ["purchase"] = GameStorageJsonCodecPurchase.Serialize(purchase),
                ["account"] = GameStorageJsonCodecAccount.Serialize(account),
            };
            return root.ToString();
        }

        public static void DeserializeInto(
            string json,
            InventoryStorage inventory,
            PurchaseStorage purchase,
            AccountStorage account)
        {
            var root = JObject.Parse(json);
            var version = root.Value<int?>("version") ?? 0;
            if (!isSupportedVersion(version))
                return;

            if (root["inventory"] is JObject inventoryObj)
                GameStorageJsonCodecInventory.DeserializeInto(inventoryObj, inventory);

            if (version >= 2 && root["purchase"] is JObject purchaseObj)
                GameStorageJsonCodecPurchase.DeserializeInto(purchaseObj, purchase);
            else
                purchase.ClearAll();

            if (version >= 10 && root["account"] is JObject accountObj)
                GameStorageJsonCodecAccount.DeserializeInto(accountObj, account);
            else
                account?.Clear();
        }

        static bool isSupportedVersion(int version)
            => version == 1 || version == 2 || version == 3 || version == 4 || version == 5 || version == 6 || version == 7 || version == 8 || version == 9 || version == CurrentVersion;
    }
}
