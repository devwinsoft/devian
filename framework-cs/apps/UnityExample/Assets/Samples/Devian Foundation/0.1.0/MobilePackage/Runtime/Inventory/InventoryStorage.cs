using System.Collections.Generic;
using System.Linq;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class InventoryTreasureCurrent
    {
        public int Exp { get; internal set; }
        public int Level { get; internal set; } = 1;

        internal void Reset(int level = 1, int exp = 0)
        {
            Level = level < 1 ? 1 : level;
            Exp = exp < 0 ? 0 : exp;
        }

        internal void Clear()
        {
            Exp = 0;
            Level = 1;
        }
    }

    public sealed class InventoryStorage
    {
        // ── Currency ──
        readonly Dictionary<CURRENCY_TYPE, long> mCurrencyBalances = new();

        // ── Equips ──
        readonly Dictionary<string, AbilityItemEquip> mEquipments = new();
        public IReadOnlyDictionary<string, AbilityItemEquip> Equipments => mEquipments;

        // ── Cards ──
        readonly Dictionary<string, AbilityItemCard> mCards = new();
        public IReadOnlyDictionary<string, AbilityItemCard> Cards => mCards;

        // ── Materials ──
        readonly Dictionary<string, AbilityItemMaterial> mMaterials = new();
        public IReadOnlyDictionary<string, AbilityItemMaterial> Materials => mMaterials;

        // ── Heroes ──
        readonly Dictionary<string, AbilityItemHero> mHeroes = new();
        public IReadOnlyDictionary<string, AbilityItemHero> Heroes => mHeroes;

        // ── Rentals ──
        readonly Dictionary<string, long> mRentals = new();
        public IReadOnlyDictionary<string, long> Rentals => mRentals;

        // ── Passes ──
        readonly Dictionary<string, bool> mPasses = new();
        public IReadOnlyDictionary<string, bool> Passes => mPasses;

        // ── Treasure ──
        readonly InventoryTreasureCurrent mTreasureCurrent = new();
        readonly Dictionary<TREASURE_GRADE_TYPE, int> mTreasureCounts = new();
        public InventoryTreasureCurrent TreasureCurrent => mTreasureCurrent;
        public IReadOnlyDictionary<TREASURE_GRADE_TYPE, int> TreasureCounts => mTreasureCounts;

        // ── Stamina ──
        public long LastStaminaUpdateUtcMs { get; internal set; }

        // ── Currency Operations ──

        public long GetCurrencyAmount(CURRENCY_TYPE currencyType)
        {
            return currencyType switch
            {
                CURRENCY_TYPE.ADS => 0L,
                CURRENCY_TYPE.ARENA_COIN => getCurrencyOrZero(CURRENCY_TYPE.ARENA_COIN),
                CURRENCY_TYPE.FREE => 0L,
                CURRENCY_TYPE.FRIENDSHIP => getCurrencyOrZero(CURRENCY_TYPE.FRIENDSHIP),
                CURRENCY_TYPE.GOLD => getCurrencyOrZero(CURRENCY_TYPE.GOLD),
                CURRENCY_TYPE.GUILD_COIN => getCurrencyOrZero(CURRENCY_TYPE.GUILD_COIN),
                CURRENCY_TYPE.JEWEL => GetCurrencyAmount(CURRENCY_TYPE.JEWEL_FREE) + GetCurrencyAmount(CURRENCY_TYPE.JEWEL_PAID),
                CURRENCY_TYPE.JEWEL_FREE => getCurrencyOrZero(CURRENCY_TYPE.JEWEL_FREE),
                CURRENCY_TYPE.JEWEL_PAID => getCurrencyOrZero(CURRENCY_TYPE.JEWEL_PAID),
                CURRENCY_TYPE.STAMINA => getCurrencyOrZero(CURRENCY_TYPE.STAMINA),
                _ => 0L,
            };
        }

        internal bool TryAddCurrency(CURRENCY_TYPE currencyType, long amount)
        {
            if (currencyType == CURRENCY_TYPE.ADS
                || currencyType == CURRENCY_TYPE.FREE
                || currencyType == CURRENCY_TYPE.JEWEL)
                return false;

            mCurrencyBalances.TryGetValue(currencyType, out var current);
            mCurrencyBalances[currencyType] = current + amount;
            return true;
        }

        internal IEnumerable<KeyValuePair<CURRENCY_TYPE, long>> EnumerateCurrencyBalancesForSave()
        {
            foreach (var kv in mCurrencyBalances)
            {
                if (kv.Key == CURRENCY_TYPE.ADS
                    || kv.Key == CURRENCY_TYPE.FREE
                    || kv.Key == CURRENCY_TYPE.JEWEL)
                    continue;

                yield return kv;
            }
        }

        // ── Equip Operations ──

        public AbilityItemEquip GetEquip(string itemUid)
        {
            return mEquipments.TryGetValue(itemUid, out var equip) ? equip : null;
        }

        public List<AbilityItemEquip> GetEquipsByItemId(string itemId)
        {
            return mEquipments.Values.Where(e => e.ItemId == itemId).ToList();
        }

        public AbilityItemEquip AddEquip(string itemUid, AbilityItemEquip ability)
        {
            if (mEquipments.ContainsKey(itemUid)) return mEquipments[itemUid];
            mEquipments[itemUid] = ability;
            return ability;
        }

        public bool RemoveEquip(string itemUid)
        {
            if (!mEquipments.TryGetValue(itemUid, out var equip)) return false;
            if (equip.IsEquipped)
            {
                if (mHeroes.TryGetValue(equip.OwnerUnitId, out var hero))
                    hero._RemoveEquip(equip.OwnerSlotType);
                else
                    equip.ClearOwner();
            }
            mEquipments.Remove(itemUid);
            return true;
        }

        // ── Card Operations ──

        public AbilityItemCard GetCard(string itemId)
        {
            return mCards.TryGetValue(itemId, out var card) ? card : null;
        }

        public AbilityItemCard AddCard(string itemId, AbilityItemCard ability)
        {
            if (mCards.ContainsKey(itemId)) return mCards[itemId];
            mCards[itemId] = ability;
            return ability;
        }

        public bool RemoveCard(string itemId)
        {
            return mCards.Remove(itemId);
        }

        // ── Material Operations ──

        public AbilityItemMaterial GetMaterial(string itemId)
        {
            return mMaterials.TryGetValue(itemId, out var material) ? material : null;
        }

        public AbilityItemMaterial AddMaterial(string itemId, AbilityItemMaterial ability)
        {
            if (mMaterials.ContainsKey(itemId)) return mMaterials[itemId];
            mMaterials[itemId] = ability;
            return ability;
        }

        public bool RemoveMaterial(string itemId)
        {
            return mMaterials.Remove(itemId);
        }

        // ── Hero Operations ──

        public AbilityItemHero GetHero(string heroId)
        {
            return mHeroes.TryGetValue(heroId, out var hero) ? hero : null;
        }

        public AbilityItemHero AddHero(string heroId, AbilityItemHero ability)
        {
            if (mHeroes.ContainsKey(heroId)) return mHeroes[heroId];
            mHeroes[heroId] = ability;
            return ability;
        }

        public bool RemoveHero(string heroId)
        {
            if (!mHeroes.TryGetValue(heroId, out var hero))
                return false;

            var equippedSlots = hero.Equips.Keys.ToList();
            for (var i = 0; i < equippedSlots.Count; i++)
                hero._RemoveEquip(equippedSlots[i]);

            return mHeroes.Remove(heroId);
        }

        // ── Hero Equip Operations ──

        public bool Equip(string heroId, SLOT_TYPE equipSlot, string equipUid)
        {
            if (!mHeroes.TryGetValue(heroId, out var hero)) return false;
            if (!mEquipments.TryGetValue(equipUid, out var equip)) return false;

            if (equip.IsEquipped)
            {
                if (mHeroes.TryGetValue(equip.OwnerUnitId, out var prevHero))
                    prevHero._RemoveEquip(equip.OwnerSlotType);
                else
                    equip.ClearOwner();
            }

            return hero._SetEquip(equip, equipSlot);
        }

        public bool Unequip(string heroId, SLOT_TYPE equipSlot)
        {
            if (!mHeroes.TryGetValue(heroId, out var hero)) return false;
            return hero._RemoveEquip(equipSlot);
        }

        // ── Rental Operations ──

        public void SetRental(string id, long expiresAtClientUtcMs)
        {
            mRentals[id] = expiresAtClientUtcMs;
        }

        public long GetRentalExpiry(string id)
        {
            return mRentals.TryGetValue(id, out var expiry) ? expiry : 0L;
        }

        public bool HasActiveRental(string id)
        {
            if (!mRentals.TryGetValue(id, out var expiry)) return false;
            return System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < expiry;
        }

        public long GetRentalRemainingMs(string id)
        {
            if (!mRentals.TryGetValue(id, out var expiry)) return 0L;
            if (expiry == long.MaxValue) return long.MaxValue;
            var remaining = expiry - System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return remaining > 0L ? remaining : 0L;
        }

        public void RemoveRental(string id)
        {
            mRentals.Remove(id);
        }

        // ── Pass Operations ──

        public bool SetPass(string id, bool owned)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            if (!owned)
                return mPasses.Remove(id);

            if (mPasses.TryGetValue(id, out var currentOwned) && currentOwned)
                return false;

            mPasses[id] = true;
            return true;
        }

        public bool HasPass(string id)
        {
            return mPasses.TryGetValue(id, out var owned) && owned;
        }

        public bool RemovePass(string id)
        {
            return mPasses.Remove(id);
        }

        // ── Treasure Operations ──

        public int GetTreasureCount(TREASURE_GRADE_TYPE gradeType)
        {
            return mTreasureCounts.TryGetValue(gradeType, out var count) ? count : 0;
        }

        internal void AddTreasure(TREASURE_GRADE_TYPE gradeType, int amount)
        {
            if (amount <= 0)
                return;

            mTreasureCounts.TryGetValue(gradeType, out var current);
            mTreasureCounts[gradeType] = current + amount;
        }

        internal void SetTreasureCount(TREASURE_GRADE_TYPE gradeType, int count)
        {
            if (count < 0)
                count = 0;

            mTreasureCounts[gradeType] = count;
        }

        internal void AddTreasureExp(int amount)
        {
            if (amount <= 0)
                return;

            mTreasureCurrent.Exp += amount;
        }

        internal void ResetTreasure(int level = 1, int exp = 0)
        {
            mTreasureCurrent.Reset(level, exp);
        }

        internal void ClearTreasureState()
        {
            mTreasureCounts.Clear();
            mTreasureCurrent.Clear();
        }

        internal void SetTreasureCurrentState(int level, int exp)
        {
            mTreasureCurrent.Reset(level, exp);
        }

        // ── Clear ──

        internal void Clear()
        {
            mCurrencyBalances.Clear();
            mCards.Clear();
            mMaterials.Clear();
            mHeroes.Clear();
            foreach (var kv in mEquipments)
                kv.Value.ClearOwner();
            mEquipments.Clear();
            mRentals.Clear();
            mPasses.Clear();
            ClearTreasureState();
            LastStaminaUpdateUtcMs = 0L;
        }

        internal void CopyFrom(InventoryStorage source)
        {
            if (ReferenceEquals(source, this))
                return;

            Clear();
            if (source == null)
                return;

            foreach (var kv in source.mCurrencyBalances)
                mCurrencyBalances[kv.Key] = kv.Value;

            foreach (var kv in source.mEquipments)
                mEquipments[kv.Key] = kv.Value;

            foreach (var kv in source.mCards)
                mCards[kv.Key] = kv.Value;

            foreach (var kv in source.mMaterials)
                mMaterials[kv.Key] = kv.Value;

            foreach (var kv in source.mHeroes)
                mHeroes[kv.Key] = kv.Value;

            foreach (var kv in source.mRentals)
                mRentals[kv.Key] = kv.Value;

            foreach (var kv in source.mPasses)
                mPasses[kv.Key] = kv.Value;

            foreach (var kv in source.mTreasureCounts)
                mTreasureCounts[kv.Key] = kv.Value;

            mTreasureCurrent.Reset(source.mTreasureCurrent.Level, source.mTreasureCurrent.Exp);
            LastStaminaUpdateUtcMs = source.LastStaminaUpdateUtcMs;
        }

        long getCurrencyOrZero(CURRENCY_TYPE currencyType)
        {
            return mCurrencyBalances.TryGetValue(currencyType, out var value) ? value : 0L;
        }
    }
}
