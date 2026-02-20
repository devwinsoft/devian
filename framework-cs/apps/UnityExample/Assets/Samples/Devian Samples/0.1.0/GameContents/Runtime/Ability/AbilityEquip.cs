using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityEquip : AbilityBase
    {
        EQUIP mTable = null;
        string mItemUid = string.Empty;
        string mOwnerUnitId = string.Empty;
        int mOwnerSlotNumber = 0;

        public string ItemUid => mItemUid;
        public string EquipId => mTable?.EquipId ?? string.Empty;
        public string OwnerUnitId => mOwnerUnitId;
        public int OwnerSlotNumber => mOwnerSlotNumber;
        public bool IsEquipped => mOwnerSlotNumber > 0;

        public void Init(EQUIP table, string itemUid)
        {
            mTable = table;
            mItemUid = itemUid;
        }

        public override AbilityBase Clone()
        {
            var c = new AbilityEquip();
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
