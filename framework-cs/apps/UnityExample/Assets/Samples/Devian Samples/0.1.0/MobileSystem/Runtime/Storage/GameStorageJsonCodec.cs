using Newtonsoft.Json.Linq;

namespace Devian
{
    internal static class GameStorageJsonCodec
    {
        const int CurrentVersion = 8;

        public static string Serialize(InventoryStorage inventory, PurchaseStorage purchase)
        {
            var root = new JObject
            {
                ["version"] = CurrentVersion,
                ["inventory"] = GameStorageJsonCodecInventory.Serialize(inventory),
                ["purchase"] = GameStorageJsonCodecPurchase.Serialize(purchase),
            };
            return root.ToString();
        }

        public static void DeserializeInto(string json, InventoryStorage inventory, PurchaseStorage purchase)
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
        }

        static bool isSupportedVersion(int version)
            => version == 1 || version == 2 || version == 3 || version == 4 || version == 5 || version == 6 || version == 7 || version == CurrentVersion;
    }
}
