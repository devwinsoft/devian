using System.Collections.Generic;
using System.Linq;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class InventoryStorage
    {
        // ── Currency ──
        readonly Dictionary<CURRENCY_TYPE, long> mWallet = new();
        public IReadOnlyDictionary<CURRENCY_TYPE, long> Wallet => mWallet;

        // ── Equips ──
        readonly Dictionary<string, AbilityEquip> mEquipments = new();
        public IReadOnlyDictionary<string, AbilityEquip> Equipments => mEquipments;

        // ── Cards ──
        readonly Dictionary<string, AbilityCard> mCards = new();
        public IReadOnlyDictionary<string, AbilityCard> Cards => mCards;

        // ── Heroes ──
        readonly Dictionary<string, AbilityUnitHero> mHeroes = new();
        public IReadOnlyDictionary<string, AbilityUnitHero> Heroes => mHeroes;

        // ── Rentals ──
        readonly Dictionary<string, long> mRentals = new();
        public IReadOnlyDictionary<string, long> Rentals => mRentals;

        // ── Passes ──
        readonly Dictionary<string, bool> mPasses = new();
        public IReadOnlyDictionary<string, bool> Passes => mPasses;

        // ── Currency Operations ──

        public long GetCurrencyBalance(CURRENCY_TYPE currencyType)
        {
            return mWallet.TryGetValue(currencyType, out var balance) ? balance : 0L;
        }

        public void AddCurrency(CURRENCY_TYPE currencyType, long amount)
        {
            mWallet.TryGetValue(currencyType, out var current);
            mWallet[currencyType] = current + amount;
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

        // ── Clear ──

        public void Clear()
        {
            mWallet.Clear();
            mCards.Clear();
            mHeroes.Clear();
            foreach (var kv in mEquipments)
                kv.Value.ClearOwner();
            mEquipments.Clear();
            mRentals.Clear();
            mPasses.Clear();
        }
    }
}
