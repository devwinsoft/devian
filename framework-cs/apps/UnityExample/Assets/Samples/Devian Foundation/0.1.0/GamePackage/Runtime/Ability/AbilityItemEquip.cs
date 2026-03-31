using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityItemEquip : AbilityItemBase
    {
        ITEM_EQUIP mTable = null;
        ITEM_EQUIP_LEVEL mLevelTable = null;
        string mItemUid = string.Empty;
        string mOwnerUnitId = string.Empty;
        int mOwnerSlotNumber = 0;

        public string ItemUid => mItemUid;
        public override string ItemId => mTable?.ItemId ?? string.Empty;
        public string OwnerUnitId => mOwnerUnitId;
        public int OwnerSlotNumber => mOwnerSlotNumber;
        public bool IsEquipped => mOwnerSlotNumber > 0;

        public void Init(ITEM_EQUIP table, ITEM_EQUIP_LEVEL levelTable, string itemUid)
        {
            mTable = table;
            mLevelTable = levelTable;
            mItemUid = itemUid;

            if (levelTable == null)
                return;

            InitLevelStats(
                levelTable.ItemLevel,
                levelTable.StatType00, levelTable.StatValue00,
                levelTable.StatType01, levelTable.StatValue01,
                levelTable.StatType02, levelTable.StatValue02,
                levelTable.StatType03, levelTable.StatValue03);
        }

        public override AbilityBase Clone()
        {
            var c = new AbilityItemEquip();
            c.mTable = mTable;
            c.mLevelTable = mLevelTable;
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
