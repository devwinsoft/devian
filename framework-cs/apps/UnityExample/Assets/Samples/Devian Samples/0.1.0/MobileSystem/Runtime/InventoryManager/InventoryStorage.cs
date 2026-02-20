using System.Collections.Generic;

namespace Devian
{
    public sealed class InventoryStorage
    {
        // ── Currency ──
        readonly Dictionary<string, long> mCurrencyBalances = new();
        public IReadOnlyDictionary<string, long> CurrencyBalances => mCurrencyBalances;

        // ── Bag ──
        readonly Dictionary<string, ItemData> mBagItems = new();
        public IReadOnlyDictionary<string, ItemData> BagItems => mBagItems;

        // ── Equipment Slots ──
        readonly Dictionary<int, string> mEquipmentSlots = new();
        public IReadOnlyDictionary<int, string> EquipmentSlots => mEquipmentSlots;

        // ── Currency Operations ──

        public long GetCurrencyBalance(string currencyId)
        {
            return mCurrencyBalances.TryGetValue(currencyId, out var balance) ? balance : 0L;
        }

        public void AddCurrency(string currencyId, long amount)
        {
            mCurrencyBalances.TryGetValue(currencyId, out var current);
            mCurrencyBalances[currencyId] = current + amount;
        }

        // ── Bag Operations ──

        public ItemData GetItem(string itemId)
        {
            return mBagItems.TryGetValue(itemId, out var data) ? data : null;
        }

        public ItemData AddItem(string itemId, ItemAbility ability)
        {
            if (mBagItems.ContainsKey(itemId)) return mBagItems[itemId];
            var data = new ItemData(ability);
            mBagItems[itemId] = data;
            return data;
        }

        public bool RemoveItem(string itemId)
        {
            if (!mBagItems.TryGetValue(itemId, out var data)) return false;
            if (data.IsEquipped) UnequipItem(itemId);
            mBagItems.Remove(itemId);
            return true;
        }

        // ── Equipment Operations ──

        public bool EquipItem(string itemId, int slotNumber)
        {
            if (!mBagItems.TryGetValue(itemId, out var data)) return false;
            if (data.IsEquipped) UnequipItem(itemId);
            if (mEquipmentSlots.ContainsKey(slotNumber))
                UnequipSlot(slotNumber);
            data.SetEquippedSlot(slotNumber);
            mEquipmentSlots[slotNumber] = itemId;
            return true;
        }

        public bool UnequipItem(string itemId)
        {
            if (!mBagItems.TryGetValue(itemId, out var data)) return false;
            if (!data.IsEquipped) return false;
            mEquipmentSlots.Remove(data.EquippedSlotNumber);
            data.ClearEquippedSlot();
            return true;
        }

        public bool UnequipSlot(int slotNumber)
        {
            if (!mEquipmentSlots.TryGetValue(slotNumber, out var itemId)) return false;
            if (mBagItems.TryGetValue(itemId, out var data))
                data.ClearEquippedSlot();
            mEquipmentSlots.Remove(slotNumber);
            return true;
        }

        public void Clear()
        {
            mCurrencyBalances.Clear();
            mEquipmentSlots.Clear();
            foreach (var kv in mBagItems)
                kv.Value.ClearEquippedSlot();
            mBagItems.Clear();
        }
    }
}
