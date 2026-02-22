using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class GameStorageManager : CompoSingleton<GameStorageManager>
    {
        const int CurrentVersion = 2;

        InventoryStorage _inventory => InventoryManager.Instance.Storage;
        readonly PurchaseStorage _purchase = new();

        public PurchaseStorage Purchase => _purchase;

        public string ToJson()
        {
            var root = new JObject();
            root["version"] = CurrentVersion;
            root["inventory"] = _serializeInventory();
            root["purchase"] = _serializePurchase();
            return root.ToString();
        }

        public void LoadFromPayload(string payload)
        {
            var json = ComplexUtil.Decrypt_Base64(payload);
            LoadFromJson(json);
        }

        public void LoadFromJson(string json)
        {
            var root = JObject.Parse(json);
            var version = root.Value<int?>("version") ?? 0;
            if (version != 1 && version != CurrentVersion)
                return;

            if (root["inventory"] is JObject inventoryObj)
                _deserializeInventory(inventoryObj);

            if (version >= 2 && root["purchase"] is JObject purchaseObj)
                _deserializePurchase(purchaseObj);
            else
                _purchase.ClearAll();
        }

        public void Clear()
        {
            _inventory.Clear();
            _purchase.ClearAll();
        }

        // ── Serialize ──

        JObject _serializeInventory()
        {
            var inventory = _inventory;
            var inv = new JObject();

            // wallet
            var walletObj = new JObject();
            foreach (var kv in inventory.Wallet)
                walletObj[kv.Key.ToString()] = kv.Value;
            inv["wallet"] = walletObj;

            // equipments
            var equipsObj = new JObject();
            foreach (var kv in inventory.Equipments)
            {
                var e = kv.Value;
                var obj = new JObject
                {
                    ["equipId"] = e.EquipId,
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
                    ["cardId"] = c.CardId,
                };
                var stats = new JObject();
                foreach (var s in c.GetStats())
                    stats[s.Key.ToString()] = s.Value;
                obj["stats"] = stats;
                cardsObj[kv.Key] = obj;
            }
            inv["cards"] = cardsObj;

            // heroes
            var heroesObj = new JObject();
            foreach (var kv in inventory.Heroes)
            {
                var h = kv.Value;
                var obj = new JObject
                {
                    ["unitId"] = h.UnitId,
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

            return inv;
        }

        JObject _serializePurchase()
        {
            var purchase = _purchase;

            var current = new JObject
            {
                ["isPurchaseInProgress"] = purchase.IsPurchaseInProgress,
                ["internalProductId"] = purchase.CurrentInternalProductId,
                ["kind"] = purchase.CurrentKind,
                ["storeKey"] = purchase.CurrentStoreKey,
                ["startedAtUtcMs"] = purchase.CurrentStartedAtUtcMs,
                ["isStorePending"] = purchase.CurrentStorePending,
                ["storePendingAtUtcMs"] = purchase.CurrentStorePendingAtUtcMs,
            };

            var last = new JObject
            {
                ["internalProductId"] = purchase.LastInternalProductId,
                ["kind"] = purchase.LastKind,
                ["storeKey"] = purchase.LastStoreKey,
                ["resultStatus"] = purchase.LastResultStatus,
                ["errorCode"] = purchase.LastErrorCode,
                ["errorMessage"] = purchase.LastErrorMessage,
                ["updatedAtUtcMs"] = purchase.LastUpdatedAtUtcMs,
            };

            return new JObject
            {
                ["current"] = current,
                ["last"] = last,
            };
        }

        // ── Deserialize ──

        void _deserializeInventory(JObject inv)
        {
            var inventory = _inventory;
            inventory.Clear();

            // wallet
            if (inv["wallet"] is JObject walletObj)
            {
                foreach (var prop in walletObj.Properties())
                {
                    if (System.Enum.TryParse<CURRENCY_TYPE>(prop.Name, out var currencyType))
                        inventory.AddCurrency(currencyType, prop.Value.Value<long>());
                }
            }

            // equipments
            if (inv["equipments"] is JObject equipsObj)
            {
                foreach (var prop in equipsObj.Properties())
                {
                    var obj = (JObject)prop.Value;
                    var equipId = obj.Value<string>("equipId");
                    var itemUid = obj.Value<string>("itemUid");

                    var ability = new AbilityEquip();
                    var table = TB_EQUIP.Get(equipId);
                    if (table != null)
                        ability.Init(table, itemUid);

                    if (obj["stats"] is JObject statsObj)
                    {
                        foreach (var sp in statsObj.Properties())
                        {
                            if (System.Enum.TryParse<STAT_TYPE>(sp.Name, out var statType))
                                ability.SetStat(statType, sp.Value.Value<int>());
                        }
                    }

                    var ownerUnitId = obj.Value<string>("ownerUnitId") ?? string.Empty;
                    var ownerSlot = obj.Value<int>("ownerSlotNumber");
                    if (ownerSlot > 0 && !string.IsNullOrEmpty(ownerUnitId))
                        ability.SetOwner(ownerUnitId, ownerSlot);

                    inventory.AddEquip(itemUid, ability);
                }
            }

            // cards
            if (inv["cards"] is JObject cardsObj)
            {
                foreach (var prop in cardsObj.Properties())
                {
                    var obj = (JObject)prop.Value;
                    var cardId = obj.Value<string>("cardId");

                    var ability = new AbilityCard();
                    var table = TB_CARD.Get(cardId);
                    if (table != null)
                        ability.Init(table);

                    if (obj["stats"] is JObject statsObj)
                    {
                        foreach (var sp in statsObj.Properties())
                        {
                            if (System.Enum.TryParse<STAT_TYPE>(sp.Name, out var statType))
                                ability.SetStat(statType, sp.Value.Value<int>());
                        }
                    }

                    inventory.AddCard(cardId, ability);
                }
            }

            // heroes (last: equip slot references need equipments)
            if (inv["heroes"] is JObject heroesObj)
            {
                foreach (var prop in heroesObj.Properties())
                {
                    var obj = (JObject)prop.Value;
                    var unitId = obj.Value<string>("unitId");

                    var ability = new AbilityUnitHero();
                    var table = TB_UNIT_HERO.Get(unitId);
                    if (table != null)
                        ability.Init(table);

                    ability.ClearStats();
                    if (obj["stats"] is JObject statsObj)
                    {
                        foreach (var sp in statsObj.Properties())
                        {
                            if (System.Enum.TryParse<STAT_TYPE>(sp.Name, out var statType))
                                ability.SetStat(statType, sp.Value.Value<int>());
                        }
                    }

                    inventory.AddHero(unitId, ability);

                    if (obj["equips"] is JObject equipsMap)
                    {
                        foreach (var ep in equipsMap.Properties())
                        {
                            if (int.TryParse(ep.Name, out var slotNumber))
                            {
                                var equipUid = ep.Value.Value<string>();
                                inventory.Equip(unitId, slotNumber, equipUid);
                            }
                        }
                    }
                }
            }
        }

        void _deserializePurchase(JObject purchaseObj)
        {
            _purchase.ClearAll();

            if (purchaseObj["current"] is JObject currentObj)
            {
                _purchase.RestoreCurrent(
                    currentObj.Value<bool?>("isPurchaseInProgress") ?? false,
                    currentObj.Value<string>("internalProductId") ?? string.Empty,
                    currentObj.Value<string>("kind") ?? string.Empty,
                    currentObj.Value<string>("storeKey") ?? string.Empty,
                    currentObj.Value<long?>("startedAtUtcMs") ?? 0L,
                    currentObj.Value<bool?>("isStorePending") ?? false,
                    currentObj.Value<long?>("storePendingAtUtcMs") ?? 0L);
            }

            if (purchaseObj["last"] is JObject lastObj)
            {
                _purchase.RestoreLast(
                    lastObj.Value<string>("internalProductId") ?? string.Empty,
                    lastObj.Value<string>("kind") ?? string.Empty,
                    lastObj.Value<string>("storeKey") ?? string.Empty,
                    lastObj.Value<string>("resultStatus") ?? string.Empty,
                    lastObj.Value<string>("errorCode") ?? string.Empty,
                    lastObj.Value<string>("errorMessage") ?? string.Empty,
                    lastObj.Value<long?>("updatedAtUtcMs") ?? 0L);
            }
        }
    }
}
