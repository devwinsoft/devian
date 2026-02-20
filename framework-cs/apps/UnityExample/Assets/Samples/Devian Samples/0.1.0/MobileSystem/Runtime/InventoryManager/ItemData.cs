using Devian.Domain.Game;

namespace Devian
{
    public sealed class ItemData
    {
        public string ItemId => mItemAbility.ItemId;
        public ItemAbility Ability => mItemAbility;
        public int Amount => mItemAbility[StatType.ItemAmount];
        public int EquippedSlotNumber => mItemAbility[StatType.ItemSlotNumber];
        public bool IsEquipped => mItemAbility[StatType.ItemSlotNumber] > 0;

        ItemAbility mItemAbility;

        public ItemData(ItemAbility ability)
        {
            mItemAbility = ability;
        }

        internal void AddAmount(int delta)
        {
            mItemAbility.AddStat(StatType.ItemAmount, delta);
        }

        internal void SetEquippedSlot(int slotNumber)
        {
            mItemAbility.SetStat(StatType.ItemSlotNumber, slotNumber);
        }

        internal void ClearEquippedSlot()
        {
            mItemAbility.ClearStat(StatType.ItemSlotNumber);
        }
    }
}
