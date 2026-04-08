using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityItemEquip : AbilityItemBase
    {
        ITEM_EQUIP mTable = null;
        ITEM_EQUIP_LEVEL mLevelTable = null;
        string mItemUid = string.Empty;
        string mOwnerUnitId = string.Empty;
        EQUIP_SLOT_TYPE mOwnerSlotType = EQUIP_SLOT_TYPE.NONE;

        public string ItemUid => mItemUid;
        public override string ItemId => mTable?.item_id ?? string.Empty;
        public EQUIP_TYPE EquipType => mTable != null ? mTable.equip_type : default;
        public string OwnerUnitId => mOwnerUnitId;
        public EQUIP_SLOT_TYPE OwnerSlotType => mOwnerSlotType;
        public bool IsEquipped => mOwnerSlotType != EQUIP_SLOT_TYPE.NONE;

        public void Init(ITEM_EQUIP table, ITEM_EQUIP_LEVEL levelTable, string itemUid)
        {
            mTable = table;
            mLevelTable = levelTable;
            mItemUid = itemUid;

            if (levelTable == null)
                return;

            InitLevelStats(
                levelTable.item_level,
                levelTable.stat_type00, levelTable.stat_value00,
                levelTable.stat_type01, levelTable.stat_value01,
                levelTable.stat_type02, levelTable.stat_value02,
                levelTable.stat_type03, levelTable.stat_value03);
        }

        public override AbilityBase Clone()
        {
            var c = new AbilityItemEquip();
            c.mTable = mTable;
            c.mLevelTable = mLevelTable;
            c.mItemUid = mItemUid;
            c.mOwnerUnitId = mOwnerUnitId;
            c.mOwnerSlotType = mOwnerSlotType;
            c.CopyStatsFrom(this);
            return c;
        }

        public static bool IsSame(AbilityItemEquip left, AbilityItemEquip right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (left == null || right == null)
                return false;

            return !string.IsNullOrWhiteSpace(left.ItemUid)
                && left.ItemUid == right.ItemUid;
        }

        internal void SetOwner(string unitId, EQUIP_SLOT_TYPE slotType)
        {
            mOwnerUnitId = unitId;
            mOwnerSlotType = slotType;
        }

        internal void ClearOwner()
        {
            mOwnerUnitId = string.Empty;
            mOwnerSlotType = EQUIP_SLOT_TYPE.NONE;
        }

        internal GameResult _LevelUp()
        {
            if (mTable == null || mLevelTable == null)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"AbilityItemEquip._LevelUp: equip is not initialized. item_id={ItemId}, itemUid={ItemUid}");
            }

            var nextLevelTable = AbilityItemFactory.ResolveNextEquipLevelTable(ItemId, mLevelTable.item_level);
            if (nextLevelTable.IsFailure)
                return GameResult.Failure(nextLevelTable.Error!);

            var currentLevelTable = mLevelTable;
            var next = nextLevelTable.Value;
            ReplaceLevelStats(
                next.item_level,
                currentLevelTable.stat_type00, currentLevelTable.stat_value00,
                currentLevelTable.stat_type01, currentLevelTable.stat_value01,
                currentLevelTable.stat_type02, currentLevelTable.stat_value02,
                currentLevelTable.stat_type03, currentLevelTable.stat_value03,
                next.stat_type00, next.stat_value00,
                next.stat_type01, next.stat_value01,
                next.stat_type02, next.stat_value02,
                next.stat_type03, next.stat_value03);
            mLevelTable = next;
            return GameResult.Ok();
        }
    }
}
