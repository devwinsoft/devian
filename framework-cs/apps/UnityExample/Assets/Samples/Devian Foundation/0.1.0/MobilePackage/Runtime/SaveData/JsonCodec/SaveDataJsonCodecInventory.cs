using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Devian.Domain.Common;
using Devian.Domain.Game;

namespace Devian
{
    internal static class SaveDataJsonCodecInventory
    {
        public static JObject Serialize(InventoryStorage inventory)
        {
            var inv = new JObject();

            // wallet
            var walletObj = new JObject();
            foreach (var kv in inventory.Wallet.EnumerateForSave())
                walletObj[kv.Key.ToString()] = kv.Value;
            inv["wallet"] = walletObj;

            // equipments
            var equipsObj = new JObject();
            foreach (var kv in inventory.Equipments)
            {
                var e = kv.Value;
                var obj = new JObject
                {
                    ["itemId"] = e.ItemId,
                    ["itemUid"] = e.ItemUid,
                    ["ownerUnitId"] = e.OwnerUnitId,
                    ["ownerSlotNumber"] = e.OwnerSlotNumber,
                };
                var stats = new JObject();
                foreach (var s in e.GetStats())
                    stats[s.Key.ToString()] = s.Value;
                obj["stats"] = stats;
                equipsObj[kv.Key] = obj;
            }
            inv["equipments"] = equipsObj;

            // cards
            var cardsObj = new JObject();
            foreach (var kv in inventory.Cards)
            {
                var c = kv.Value;
                var obj = new JObject
                {
                    ["itemId"] = c.ItemId,
                };
                var stats = new JObject();
                foreach (var s in c.GetStats())
                    stats[s.Key.ToString()] = s.Value;
                obj["stats"] = stats;
                cardsObj[kv.Key] = obj;
            }
            inv["cards"] = cardsObj;

            // materials
            var materialsObj = new JObject();
            foreach (var kv in inventory.Materials)
            {
                var m = kv.Value;
                var obj = new JObject
                {
                    ["itemId"] = m.ItemId,
                };
                var stats = new JObject();
                foreach (var s in m.GetStats())
                    stats[s.Key.ToString()] = s.Value;
                obj["stats"] = stats;
                materialsObj[kv.Key] = obj;
            }
            inv["materials"] = materialsObj;

            // heroes
            var heroesObj = new JObject();
            foreach (var kv in inventory.Heroes)
            {
                var h = kv.Value;
                var obj = new JObject
                {
                    ["heroId"] = h.HeroId,
                };
                var stats = new JObject();
                foreach (var s in h.GetStats())
                    stats[s.Key.ToString()] = s.Value;
                obj["stats"] = stats;

                var equipsMap = new JObject();
                foreach (var eq in h.Equips)
                    equipsMap[eq.Key.ToString()] = eq.Value.ItemUid;
                obj["equips"] = equipsMap;

                heroesObj[kv.Key] = obj;
            }
            inv["heroes"] = heroesObj;

            // rentals
            var rentalsObj = new JObject();
            foreach (var kv in inventory.Rentals)
                rentalsObj[kv.Key] = kv.Value;
            inv["rentals"] = rentalsObj;

            // passes
            var passesObj = new JObject();
            foreach (var kv in inventory.Passes)
                passesObj[kv.Key] = kv.Value;
            inv["passes"] = passesObj;

            // stamina
            if (inventory.LastStaminaUpdateUtcMs > 0L)
                inv["lastStaminaUpdateUtcMs"] = inventory.LastStaminaUpdateUtcMs;

            return inv;
        }

        public static CommonResult DeserializeInto(JObject inv, InventoryStorage inventory)
        {
            if (inv == null)
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "SaveDataJsonCodecInventory.DeserializeInto: inventory json is null.");
            }

            if (inventory == null)
            {
                return CommonResult.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "SaveDataJsonCodecInventory.DeserializeInto: inventory storage is null.");
            }

            inventory.Clear();

            // wallet
            if (inv["wallet"] is JObject walletObj)
            {
                foreach (var prop in walletObj.Properties())
                {
                    if (System.Enum.TryParse<CURRENCY_TYPE>(prop.Name, out var currencyType))
                    {
                        if (currencyType == CURRENCY_TYPE.ADS
                            || currencyType == CURRENCY_TYPE.FREE
                            || currencyType == CURRENCY_TYPE.JEWEL)
                            continue;
                        inventory.Wallet.TryAdd(currencyType, prop.Value.Value<long>());
                    }
                }
            }

            // equipments
            if (inv["equipments"] is JObject equipsObj)
            {
                foreach (var prop in equipsObj.Properties())
                {
                    var obj = (JObject)prop.Value;
                    var itemId = obj.Value<string>("itemId");
                    var itemUid = obj.Value<string>("itemUid");

                    var ownerUnitId = obj.Value<string>("ownerUnitId") ?? string.Empty;
                    var ownerSlot = obj.Value<int>("ownerSlotNumber");
                    var ability = AbilityItemFactory.CreateEquip(
                        itemId,
                        itemUid,
                        parseStats(obj["stats"] as JObject),
                        ownerUnitId,
                        ownerSlot);
                    if (ability.IsFailure)
                        return CommonResult.Failure(ability.Error!);

                    inventory.AddEquip(itemUid, ability.Value);
                }
            }

            // cards
            if (inv["cards"] is JObject cardsObj)
            {
                foreach (var prop in cardsObj.Properties())
                {
                    var obj = (JObject)prop.Value;
                    var itemId = obj.Value<string>("itemId");
                    var ability = AbilityItemFactory.CreateCard(itemId, parseStats(obj["stats"] as JObject));
                    if (ability.IsFailure)
                        return CommonResult.Failure(ability.Error!);

                    inventory.AddCard(itemId, ability.Value);
                }
            }

            // materials
            if (inv["materials"] is JObject materialsObj)
            {
                foreach (var prop in materialsObj.Properties())
                {
                    var obj = (JObject)prop.Value;
                    var itemId = obj.Value<string>("itemId");
                    var ability = AbilityItemFactory.CreateMaterial(itemId, parseStats(obj["stats"] as JObject));
                    if (ability.IsFailure)
                        return CommonResult.Failure(ability.Error!);

                    inventory.AddMaterial(itemId, ability.Value);
                }
            }

            // heroes (last: equip slot references need equipments)
            if (inv["heroes"] is JObject heroesObj)
            {
                foreach (var prop in heroesObj.Properties())
                {
                    var obj = (JObject)prop.Value;
                    var heroId = obj.Value<string>("heroId");
                    var ability = AbilityItemFactory.CreateHero(heroId, parseStats(obj["stats"] as JObject));
                    if (ability.IsFailure)
                        return CommonResult.Failure(ability.Error!);

                    inventory.AddHero(heroId, ability.Value);

                    if (obj["equips"] is JObject equipsMap)
                    {
                        foreach (var ep in equipsMap.Properties())
                        {
                            if (!int.TryParse(ep.Name, out var slotNumber))
                            {
                                return CommonResult.Failure(
                                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                                    $"SaveDataJsonCodecInventory.DeserializeInto: invalid hero equip slot key: {ep.Name}");
                            }

                            var equipUid = ep.Value.Value<string>();
                            if (!inventory.Equip(heroId, slotNumber, equipUid))
                            {
                                return CommonResult.Failure(
                                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                                    $"SaveDataJsonCodecInventory.DeserializeInto: failed to restore hero equip. heroId={heroId}, slot={slotNumber}, equipUid={equipUid}");
                            }
                        }
                    }
                }
            }

            // rentals
            if (inv["rentals"] is JObject rentalsObj)
            {
                foreach (var prop in rentalsObj.Properties())
                    inventory.SetRental(prop.Name, prop.Value.Value<long>());
            }

            // passes
            if (inv["passes"] is JObject passesObj)
            {
                foreach (var prop in passesObj.Properties())
                    inventory.SetPass(prop.Name, prop.Value.Value<bool>());
            }

            // stamina
            inventory.LastStaminaUpdateUtcMs = inv.Value<long?>("lastStaminaUpdateUtcMs") ?? 0L;
            return CommonResult.Ok();
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
