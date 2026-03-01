using Newtonsoft.Json.Linq;

namespace Devian
{
    internal static class SaveDataJsonCodec
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
                ["inventory"] = SaveDataJsonCodecInventory.Serialize(inventory),
                ["purchase"] = SaveDataJsonCodecPurchase.Serialize(purchase),
                ["account"] = SaveDataJsonCodecAccount.Serialize(account),
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
                SaveDataJsonCodecInventory.DeserializeInto(inventoryObj, inventory);

            if (version >= 2 && root["purchase"] is JObject purchaseObj)
                SaveDataJsonCodecPurchase.DeserializeInto(purchaseObj, purchase);
            else
                purchase.ClearAll();

            if (version >= 10 && root["account"] is JObject accountObj)
                SaveDataJsonCodecAccount.DeserializeInto(accountObj, account);
            else
                account?.Clear();
        }

        static bool isSupportedVersion(int version)
            => version == 1 || version == 2 || version == 3 || version == 4 || version == 5 || version == 6 || version == 7 || version == 8 || version == 9 || version == CurrentVersion;
    }
}
