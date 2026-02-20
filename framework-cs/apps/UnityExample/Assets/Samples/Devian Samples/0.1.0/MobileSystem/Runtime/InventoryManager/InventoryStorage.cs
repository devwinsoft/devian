using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class InventoryStorage
    {
        // ── Currency ──
        readonly Dictionary<string, long> mWallet = new();
        public IReadOnlyDictionary<string, long> Wallet => mWallet;

        // ── Equips ──
        readonly Dictionary<string, AbilityEquip> mEquipments = new();
        public IReadOnlyDictionary<string, AbilityEquip> Equipments => mEquipments;

        // ── Cards ──
        readonly Dictionary<string, AbilityCard> mCards = new();
        public IReadOnlyDictionary<string, AbilityCard> Cards => mCards;

        // ── Heroes ──
        readonly Dictionary<string, AbilityUnitHero> mHeroes = new();
        public IReadOnlyDictionary<string, AbilityUnitHero> Heroes => mHeroes;

        // ── Currency Operations ──

        public long GetCurrencyBalance(string currencyId)
        {
            return mWallet.TryGetValue(currencyId, out var balance) ? balance : 0L;
        }

        public void AddCurrency(string currencyId, long amount)
        {
            mWallet.TryGetValue(currencyId, out var current);
            mWallet[currencyId] = current + amount;
        }

        // ── Equip Operations ──

        public AbilityEquip GetEquip(string itemUid)
        {
            return mEquipments.TryGetValue(itemUid, out var equip) ? equip : null;
        }

        public List<AbilityEquip> GetEquipsByEquipId(string equipId)
        {
            return mEquipments.Values.Where(e => e.EquipId == equipId).ToList();
        }

        public AbilityEquip AddEquip(string itemUid, AbilityEquip ability)
        {
            if (mEquipments.ContainsKey(itemUid)) return mEquipments[itemUid];
            mEquipments[itemUid] = ability;
            return ability;
        }

        public bool RemoveEquip(string itemUid)
        {
            if (!mEquipments.TryGetValue(itemUid, out var equip)) return false;
            if (equip.IsEquipped) equip.ClearOwner();
            mEquipments.Remove(itemUid);
            return true;
        }

        // ── Card Operations ──

        public AbilityCard GetCard(string cardId)
        {
            return mCards.TryGetValue(cardId, out var card) ? card : null;
        }

        public AbilityCard AddCard(string cardId, AbilityCard ability)
        {
            if (mCards.ContainsKey(cardId)) return mCards[cardId];
            mCards[cardId] = ability;
            return ability;
        }

        // ── Hero Operations ──

        public AbilityUnitHero GetHero(string heroId)
        {
            return mHeroes.TryGetValue(heroId, out var hero) ? hero : null;
        }

        public AbilityUnitHero AddHero(string heroId, AbilityUnitHero ability)
        {
            if (mHeroes.ContainsKey(heroId)) return mHeroes[heroId];
            mHeroes[heroId] = ability;
            return ability;
        }

        // ── Equip Operations (장착/해제) ──

        public bool Equip(string heroId, int equipSlot, string equipUid)
        {
            if (!mHeroes.TryGetValue(heroId, out var hero)) return false;
            if (!mEquipments.TryGetValue(equipUid, out var equip)) return false;
            return hero.Equip(equip, equipSlot);
        }

        public bool Unequip(string heroId, int equipSlot)
        {
            if (!mHeroes.TryGetValue(heroId, out var hero)) return false;
            return hero.Unequip(equipSlot);
        }

        // ── JSON ──

        public string ToJson()
        {
            var root = new JObject();

            // wallet
            var walletObj = new JObject();
            foreach (var kv in mWallet)
                walletObj[kv.Key] = kv.Value;
            root["wallet"] = walletObj;

            // equipments
            var equipsObj = new JObject();
            foreach (var kv in mEquipments)
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
            root["equipments"] = equipsObj;

            // cards
            var cardsObj = new JObject();
            foreach (var kv in mCards)
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
            root["cards"] = cardsObj;

            // heroes
            var heroesObj = new JObject();
            foreach (var kv in mHeroes)
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
            root["heroes"] = heroesObj;

            return root.ToString();
        }

        public void FromJson(string json)
        {
            var root = JObject.Parse(json);
            Clear();

            // wallet
            if (root["wallet"] is JObject walletObj)
            {
                foreach (var prop in walletObj.Properties())
                    mWallet[prop.Name] = prop.Value.Value<long>();
            }

            // equipments
            if (root["equipments"] is JObject equipsObj)
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
                            if (System.Enum.TryParse<StatType>(sp.Name, out var statType))
                                ability.SetStat(statType, sp.Value.Value<int>());
                        }
                    }

                    var ownerUnitId = obj.Value<string>("ownerUnitId") ?? string.Empty;
                    var ownerSlot = obj.Value<int>("ownerSlotNumber");
                    if (ownerSlot > 0 && !string.IsNullOrEmpty(ownerUnitId))
                        ability.SetOwner(ownerUnitId, ownerSlot);

                    mEquipments[itemUid] = ability;
                }
            }

            // cards
            if (root["cards"] is JObject cardsObj)
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
                            if (System.Enum.TryParse<StatType>(sp.Name, out var statType))
                                ability.SetStat(statType, sp.Value.Value<int>());
                        }
                    }

                    mCards[cardId] = ability;
                }
            }

            // heroes (last: equip slot references need mEquipments)
            if (root["heroes"] is JObject heroesObj)
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
                            if (System.Enum.TryParse<StatType>(sp.Name, out var statType))
                                ability.SetStat(statType, sp.Value.Value<int>());
                        }
                    }

                    mHeroes[unitId] = ability;

                    if (obj["equips"] is JObject equipsMap)
                    {
                        foreach (var ep in equipsMap.Properties())
                        {
                            if (int.TryParse(ep.Name, out var slotNumber))
                            {
                                var equipUid = ep.Value.Value<string>();
                                if (mEquipments.TryGetValue(equipUid, out var equip))
                                    ability.Equip(equip, slotNumber);
                            }
                        }
                    }
                }
            }
        }

        // ── Clear ──

        public void Clear()
        {
            mWallet.Clear();
            mCards.Clear();
            mHeroes.Clear();
            foreach (var kv in mEquipments)
                kv.Value.ClearOwner();
            mEquipments.Clear();
        }
    }
}
