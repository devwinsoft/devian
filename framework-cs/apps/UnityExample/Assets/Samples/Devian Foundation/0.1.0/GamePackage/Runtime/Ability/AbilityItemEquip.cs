using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityItemEquip : AbilityItemBase
    {
        ITEM_EQUIP mTable = null;
        string mItemUid = string.Empty;
        string mOwnerUnitId = string.Empty;
        int mOwnerSlotNumber = 0;

        public string ItemUid => mItemUid;
        public override string ItemId => mTable?.ItemId ?? string.Empty;
        public string OwnerUnitId => mOwnerUnitId;
        public int OwnerSlotNumber => mOwnerSlotNumber;
        public bool IsEquipped => mOwnerSlotNumber > 0;

        public void Init(ITEM_EQUIP table, string itemUid)
        {
            mTable = table;
            mItemUid = itemUid;
        }

        public override AbilityBase Clone()
        {
            var c = new AbilityItemEquip();
            c.mTable = mTable;
            c.mItemUid = mItemUid;
            c.mOwnerUnitId = mOwnerUnitId;
            c.mOwnerSlotNumber = mOwnerSlotNumber;
            c.CopyStatsFrom(this);
            return c;
        }

        public void SetOwner(string unitId, int slotNumber)
        {
            mOwnerUnitId = unitId;
            mOwnerSlotNumber = slotNumber;
        }

        public void ClearOwner()
        {
            mOwnerUnitId = string.Empty;
            mOwnerSlotNumber = 0;
        }
    }
}
