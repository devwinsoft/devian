using System.Collections.Generic;

namespace Devian
{
    public sealed class InventoryStorage
    {
        // ── Bag ──
        readonly Dictionary<string, ItemData> mBagItems = new();
        public IReadOnlyDictionary<string, ItemData> BagItems => mBagItems;

        // ── Equipment Slots ──
        readonly Dictionary<int, ItemData> mEquipmentSlots = new();
        public IReadOnlyDictionary<int, ItemData> EquipmentSlots => mEquipmentSlots;

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

        public bool EquipItem(string itemId, int slotIndex)
        {
            if (!mBagItems.TryGetValue(itemId, out var data)) return false;
            if (data.IsEquipped) UnequipItem(itemId);
            if (mEquipmentSlots.ContainsKey(slotIndex))
                UnequipSlot(slotIndex);
            data.SetEquippedSlot(slotIndex);
            mEquipmentSlots[slotIndex] = data;
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

        public bool UnequipSlot(int slotIndex)
        {
            if (!mEquipmentSlots.TryGetValue(slotIndex, out var data)) return false;
            data.ClearEquippedSlot();
            mEquipmentSlots.Remove(slotIndex);
            return true;
        }

        public void Clear()
        {
            mEquipmentSlots.Clear();
            foreach (var kv in mBagItems)
                kv.Value.ClearEquippedSlot();
            mBagItems.Clear();
        }
    }
}
