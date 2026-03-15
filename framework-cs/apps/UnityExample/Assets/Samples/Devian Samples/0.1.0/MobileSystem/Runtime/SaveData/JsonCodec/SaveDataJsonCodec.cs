using Newtonsoft.Json.Linq;

namespace Devian
{
    internal static class SaveDataJsonCodec
    {
        const int CurrentVersion = 20;
        const int AttendVersion = 17;
        const int ShopVersion = 18;
        const int TreasureVersion = 20;

        public static string Serialize(
            InventoryStorage inventory,
            PurchaseStorage purchase,
            ShopStorage shop,
            AccountStorage account,
            GameMessageStorage message,
            MissionStorage mission,
            AchieveStorage achieve,
            LeaderboardSeasonRewardStorage leaderboardReward,
            AttendStorage attend)
        {
            var root = new JObject
            {
                ["version"] = CurrentVersion,
                ["inventory"] = SaveDataJsonCodecInventory.Serialize(inventory),
                ["purchase"] = SaveDataJsonCodecPurchase.Serialize(purchase),
                ["shop"] = SaveDataJsonCodecShop.Serialize(shop),
                ["account"] = SaveDataJsonCodecAccount.Serialize(account),
                ["message"] = SaveDataJsonCodecMessage.Serialize(message),
                ["mission"] = SaveDataJsonCodecMission.Serialize(mission),
                ["achieve"] = SaveDataJsonCodecAchieve.Serialize(achieve),
                ["leaderboardReward"] = SaveDataJsonCodecLeaderboardReward.Serialize(leaderboardReward),
                ["attend"] = SaveDataJsonCodecAttend.Serialize(attend),
                ["treasure"] = SaveDataJsonCodecTreasure.Serialize(inventory),
            };
            return root.ToString();
        }

        public static void DeserializeInto(
            string json,
            InventoryStorage inventory,
            PurchaseStorage purchase,
            ShopStorage shop,
            AccountStorage account,
            GameMessageStorage message,
            MissionStorage mission,
            AchieveStorage achieve,
            LeaderboardSeasonRewardStorage leaderboardReward,
            AttendStorage attend)
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

            if (version >= ShopVersion && root["shop"] is JObject shopObj)
                SaveDataJsonCodecShop.DeserializeInto(shopObj, shop);
            else
                shop.Clear();

            if (version >= 10 && root["account"] is JObject accountObj)
                SaveDataJsonCodecAccount.DeserializeInto(accountObj, account);
            else
                account?.Clear();

            if (version >= 13 && root["message"] is JObject messageObj)
                SaveDataJsonCodecMessage.DeserializeInto(messageObj, message);
            else
                message?.Clear();

            if (version >= 11 && root["mission"] is JObject missionObj)
            {
                SaveDataJsonCodecMission.DeserializeInto(missionObj, mission, message);
            }
            else
                mission?.Clear();

            if (version >= 12 && root["achieve"] is JObject achieveObj)
                SaveDataJsonCodecAchieve.DeserializeInto(achieveObj, achieve);
            else
                achieve?.Clear();

            if (version >= 14 && root["leaderboardReward"] is JObject leaderboardRewardObj)
                SaveDataJsonCodecLeaderboardReward.DeserializeInto(leaderboardRewardObj, leaderboardReward);
            else
                leaderboardReward?.Clear();

            if (version >= AttendVersion && root["attend"] is JObject attendObj)
                SaveDataJsonCodecAttend.DeserializeInto(attendObj, attend);
            else
                attend?.Clear();

            if (version >= TreasureVersion && root["treasure"] is JObject treasureObj)
                SaveDataJsonCodecTreasure.DeserializeInto(treasureObj, inventory);
            else
            {
                inventory?.TreasureCounts.Clear();
                inventory?.TreasureCurrent.Clear();
            }
        }

        static bool isSupportedVersion(int version)
            => version == 1 || version == 2 || version == 3 || version == 4 || version == 5 || version == 6 || version == 7 || version == 8 || version == 9 || version == 10 || version == 11 || version == 12 || version == 13 || version == 14 || version == 15 || version == 16 || version == 17 || version == 18 || version == 19 || version == CurrentVersion;
    }
}
