using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Devian.Domain.Common;
using Devian.Domain.Game;

namespace Devian
{
    internal static class SaveDataJsonCodecInventory
    {
        public static JObject Serialize(InventorySnapshot inventory)
        {
            inventory ??= new InventorySnapshot();

            var inv = new JObject();

            var walletObj = new JObject();
            foreach (var kv in inventory.CurrencyBalances)
                walletObj[kv.Key.ToString()] = kv.Value;
            inv["wallet"] = walletObj;

            var equipsObj = new JObject();
            foreach (var kv in inventory.Equipments)
            {
                var e = kv.Value;
                var obj = new JObject
                {
                    ["item_id"] = e.ItemId,
                    ["itemUid"] = e.ItemUid,
                    ["item_level"] = e.ItemLevel,
                };
                equipsObj[kv.Key] = obj;
            }
            inv["equipments"] = equipsObj;

            var cardsObj = new JObject();
            foreach (var kv in inventory.Cards)
            {
                var c = kv.Value;
                var obj = new JObject
                {
                    ["item_id"] = c.ItemId,
                    ["item_level"] = c.ItemLevel,
                    ["amount"] = c.Amount,
                };
                cardsObj[kv.Key] = obj;
            }
            inv["cards"] = cardsObj;

            var materialsObj = new JObject();
            foreach (var kv in inventory.Materials)
            {
                var m = kv.Value;
                var obj = new JObject
                {
                    ["item_id"] = m.ItemId,
                    ["amount"] = m.Amount,
                };
                materialsObj[kv.Key] = obj;
            }
            inv["materials"] = materialsObj;

            var heroesObj = new JObject();
            foreach (var kv in inventory.Heroes)
            {
                var h = kv.Value;
                var obj = new JObject
                {
                    ["item_id"] = h.ItemId,
                    ["item_level"] = h.ItemLevel,
                    ["amount"] = h.Amount,
                };

                var equipsMap = new JObject();
                foreach (var eq in h.Equips)
                    equipsMap[eq.Key.ToString()] = eq.Value;
                obj["equips"] = equipsMap;

                heroesObj[kv.Key] = obj;
            }
            inv["heroes"] = heroesObj;

            var rentalsObj = new JObject();
            foreach (var kv in inventory.Rentals)
                rentalsObj[kv.Key] = kv.Value;
            inv["rentals"] = rentalsObj;

            var passesObj = new JObject();
            foreach (var kv in inventory.Passes)
                passesObj[kv.Key] = kv.Value;
            inv["passes"] = passesObj;

            if (inventory.LastStaminaUpdateUtcMs > 0L)
                inv["lastStaminaUpdateUtcMs"] = inventory.LastStaminaUpdateUtcMs;

            return inv;
        }

        public static GameResult DeserializeInto(JObject inv, InventorySnapshot inventory)
        {
            if (inv == null)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.SAVEDATA_PAYLOAD_PARSE_FAILED,
                    "SaveDataJsonCodecInventory.DeserializeInto: inventory json is null.");
            }

            if (inventory == null)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.SAVEDATA_PAYLOAD_PARSE_FAILED,
                    "SaveDataJsonCodecInventory.DeserializeInto: inventory snapshot is null.");
            }

            inventory.ClearInventoryState();

            if (inv["wallet"] is JObject walletObj)
            {
                foreach (var prop in walletObj.Properties())
                {
                    if (!System.Enum.TryParse(prop.Name, out CURRENCY_TYPE currencyType))
                        continue;

                    if (currencyType == CURRENCY_TYPE.ADS
                        || currencyType == CURRENCY_TYPE.FREE
                        || currencyType == CURRENCY_TYPE.JEWEL)
                        continue;

                    inventory.CurrencyBalances[currencyType] = prop.Value.Value<long>();
                }
            }

            if (inv["equipments"] is JObject equipsObj)
            {
                foreach (var prop in equipsObj.Properties())
                {
                    var obj = (JObject)prop.Value;
                    var itemId = obj.Value<string>("item_id");
                    var itemUid = obj.Value<string>("itemUid");
                    var savedStats = parseStats(obj["stats"] as JObject);
                    var itemLevel = readSavedItemLevel(obj, savedStats);

                    inventory.Equipments[prop.Name] = new InventorySnapshotEquipment
                    {
                        ItemId = itemId ?? string.Empty,
                        ItemUid = string.IsNullOrWhiteSpace(itemUid) ? prop.Name : itemUid,
                        ItemLevel = itemLevel,
                    };
                }
            }

            if (inv["cards"] is JObject cardsObj)
            {
                foreach (var prop in cardsObj.Properties())
                {
                    var obj = (JObject)prop.Value;
                    var itemId = obj.Value<string>("item_id");
                    var savedStats = parseStats(obj["stats"] as JObject);
                    var itemLevel = readSavedItemLevel(obj, savedStats);
                    var amount = readSavedItemAmount(obj, savedStats);
                    if (amount < 0)
                    {
                        return GameResult.Failure(
                            GAME_ERROR_TYPE.SAVEDATA_PAYLOAD_PARSE_FAILED,
                            $"SaveDataJsonCodecInventory.DeserializeInto: card amount is negative. item_id={itemId}, amount={amount}");
                    }

                    inventory.Cards[prop.Name] = new InventorySnapshotCard
                    {
                        ItemId = itemId ?? string.Empty,
                        ItemLevel = itemLevel,
                        Amount = amount,
                    };
                }
            }

            if (inv["materials"] is JObject materialsObj)
            {
                foreach (var prop in materialsObj.Properties())
                {
                    var obj = (JObject)prop.Value;
                    var itemId = obj.Value<string>("item_id");
                    var savedStats = parseStats(obj["stats"] as JObject);
                    var amount = readSavedItemAmount(obj, savedStats);
                    if (amount < 0)
                    {
                        return GameResult.Failure(
                            GAME_ERROR_TYPE.SAVEDATA_PAYLOAD_PARSE_FAILED,
                            $"SaveDataJsonCodecInventory.DeserializeInto: material amount is negative. item_id={itemId}, amount={amount}");
                    }

                    inventory.Materials[prop.Name] = new InventorySnapshotMaterial
                    {
                        ItemId = itemId ?? string.Empty,
                        Amount = amount,
                    };
                }
            }

            if (inv["heroes"] is JObject heroesObj)
            {
                var restoredEquipUids = new HashSet<string>();
                foreach (var prop in heroesObj.Properties())
                {
                    var obj = (JObject)prop.Value;
                    var itemId = obj.Value<string>("item_id");
                    var savedStats = parseStats(obj["stats"] as JObject);
                    var itemLevel = readSavedItemLevel(obj, savedStats);
                    var amount = readSavedItemAmount(obj, savedStats);
                    if (amount < 0)
                    {
                        return GameResult.Failure(
                            GAME_ERROR_TYPE.SAVEDATA_PAYLOAD_PARSE_FAILED,
                            $"SaveDataJsonCodecInventory.DeserializeInto: hero amount is negative. item_id={itemId}, amount={amount}");
                    }

                    var heroSnapshot = new InventorySnapshotHero
                    {
                        ItemId = itemId ?? string.Empty,
                        ItemLevel = itemLevel,
                        Amount = amount,
                    };

                    if (obj["equips"] is JObject equipsMap)
                    {
                        foreach (var ep in equipsMap.Properties())
                        {
                            if (!int.TryParse(ep.Name, out var slotNumber))
                            {
                                return GameResult.Failure(
                                    GAME_ERROR_TYPE.SAVEDATA_PAYLOAD_PARSE_FAILED,
                                    $"SaveDataJsonCodecInventory.DeserializeInto: invalid hero equip slot key: {ep.Name}");
                            }

                            var equipUid = ep.Value.Value<string>();
                            if (string.IsNullOrWhiteSpace(equipUid))
                            {
                                return GameResult.Failure(
                                    GAME_ERROR_TYPE.SAVEDATA_PAYLOAD_PARSE_FAILED,
                                    $"SaveDataJsonCodecInventory.DeserializeInto: empty hero equip uid. item_id={itemId}, slot={slotNumber}");
                            }

                            if (!restoredEquipUids.Add(equipUid))
                            {
                                return GameResult.Failure(
                                    GAME_ERROR_TYPE.SAVEDATA_PAYLOAD_PARSE_FAILED,
                                    $"SaveDataJsonCodecInventory.DeserializeInto: duplicate hero equip reference. equipUid={equipUid}");
                            }

                            heroSnapshot.Equips[slotNumber] = equipUid;
                        }
                    }

                    inventory.Heroes[prop.Name] = heroSnapshot;
                }
            }

            if (inv["rentals"] is JObject rentalsObj)
            {
                foreach (var prop in rentalsObj.Properties())
                    inventory.Rentals[prop.Name] = prop.Value.Value<long>();
            }

            if (inv["passes"] is JObject passesObj)
            {
                foreach (var prop in passesObj.Properties())
                    inventory.Passes[prop.Name] = prop.Value.Value<bool>();
            }

            inventory.LastStaminaUpdateUtcMs = inv.Value<long?>("lastStaminaUpdateUtcMs") ?? 0L;
            return GameResult.Ok();
        }

        static int readSavedItemLevel(JObject obj, IReadOnlyDictionary<STAT_TYPE, int> stats)
        {
            var itemLevel = obj?.Value<int?>("item_level");
            if (itemLevel.HasValue)
                return itemLevel.Value;

            if (stats != null && stats.TryGetValue(STAT_TYPE.ITEM_LEVEL, out var compatLevel))
                return compatLevel;

            return 1;
        }

        static int readSavedItemAmount(JObject obj, IReadOnlyDictionary<STAT_TYPE, int> stats)
        {
            var amount = obj?.Value<int?>("amount");
            if (amount.HasValue)
                return amount.Value;

            if (stats != null && stats.TryGetValue(STAT_TYPE.ITEM_AMOUNT, out var compatAmount))
                return compatAmount;

            return 0;
        }

        static Dictionary<STAT_TYPE, int> parseStats(JObject statsObj)
        {
            if (statsObj == null)
                return null;

            var stats = new Dictionary<STAT_TYPE, int>();
            foreach (var sp in statsObj.Properties())
            {
                if (tryParseStatTypeCompat(sp.Name, out var statType))
                    stats[statType] = sp.Value.Value<int>();
            }

            return stats;
        }

        static bool tryParseStatTypeCompat(string name, out STAT_TYPE statType)
        {
            switch (name)
            {
                case "CARD_AMOUNT":
                case "UNIT_AMOUNT":
                    statType = STAT_TYPE.ITEM_AMOUNT;
                    return true;
                case "CARD_LEVEL":
                case "UNIT_LEVEL":
                    statType = STAT_TYPE.ITEM_LEVEL;
                    return true;
                default:
                    return System.Enum.TryParse(name, out statType);
            }
        }
    }
}
