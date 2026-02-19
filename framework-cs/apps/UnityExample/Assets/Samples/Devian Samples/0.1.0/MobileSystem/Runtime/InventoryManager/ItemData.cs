using Devian.Domain.Game;

namespace Devian
{
    public sealed class ItemData
    {
        public string ItemId => mItemAbility.ItemId;
        public ItemAbility Ability => mItemAbility;
        public long Count => mItemAbility[StatType.ItemCount];
        public int EquippedSlotNumber => mItemAbility[StatType.ItemSlotNumber];
        public bool IsEquipped => mItemAbility[StatType.ItemSlotNumber] > 0;

        ItemAbility mItemAbility;

        public ItemData(ItemAbility ability)
        {
            mItemAbility = ability;
        }

        internal void AddCount(long delta)
        {
            mItemAbility.AddStat(StatType.ItemCount, (int)delta);
        }

        internal void SetEquippedSlot(int slotIndex)
        {
            int currentSlot = mItemAbility[StatType.ItemSlotNumber];
            if (currentSlot != 0)
                mItemAbility.AddStat(StatType.ItemSlotNumber, -currentSlot);
            mItemAbility.AddStat(StatType.ItemSlotNumber, slotIndex);
        }

        internal void ClearEquippedSlot()
        {
            int currentSlot = mItemAbility[StatType.ItemSlotNumber];
            if (currentSlot != 0)
                mItemAbility.AddStat(StatType.ItemSlotNumber, -currentSlot);
        }
    }
}
