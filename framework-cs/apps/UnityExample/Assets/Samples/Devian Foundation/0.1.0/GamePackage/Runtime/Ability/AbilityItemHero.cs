using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityItemHero : AbilityItemBase
    {
        ITEM_HERO mTable = null;
        ITEM_HERO_LEVEL mLevelTable = null;
        readonly Dictionary<int, AbilityItemEquip> mEquips = new();

        public string UnitId => mTable?.unit_id ?? string.Empty;
        public override string ItemId => mTable?.item_id ?? string.Empty;
        public IReadOnlyDictionary<int, AbilityItemEquip> Equips => mEquips;

        public void Init(ITEM_HERO table, ITEM_HERO_LEVEL levelTable)
        {
            mTable = table;
            mLevelTable = levelTable;

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
            var c = new AbilityItemHero();
            c.mTable = mTable;
            c.mLevelTable = mLevelTable;
            c.CopyStatsFrom(this);
            foreach (var kv in mEquips)
                c.mEquips[kv.Key] = kv.Value;
            return c;
        }

        public bool SetEquip(AbilityItemEquip equip, int slotNumber)
        {
            if (equip == null || slotNumber <= 0)
                return false;

            if (mEquips.TryGetValue(slotNumber, out var prev))
            {
                if (isSameEquip(prev, equip))
                {
                    prev.SetOwner(ItemId, slotNumber);
                    return true;
                }

                if (prev.OwnerUnitId == ItemId && prev.OwnerSlotNumber == slotNumber)
                    prev.ClearOwner();
            }

            var existingSlot = findEquipSlot(equip);
            if (existingSlot > 0)
            {
                if (existingSlot == slotNumber)
                    return true;

                mEquips.Remove(existingSlot);
            }

            mEquips[slotNumber] = equip;
            equip.SetOwner(ItemId, slotNumber);
            return true;
        }

        public bool RemoveEquip(int slotNumber)
        {
            if (!mEquips.TryGetValue(slotNumber, out var equip))
                return false;

            if (equip.OwnerUnitId == ItemId && equip.OwnerSlotNumber == slotNumber)
                equip.ClearOwner();

            mEquips.Remove(slotNumber);
            return true;
        }

        int findEquipSlot(AbilityItemEquip equip)
        {
            foreach (var kv in mEquips)
            {
                if (isSameEquip(kv.Value, equip))
                    return kv.Key;
            }

            return 0;
        }

        static bool isSameEquip(AbilityItemEquip left, AbilityItemEquip right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (left == null || right == null)
                return false;

            return !string.IsNullOrWhiteSpace(left.ItemUid)
                && left.ItemUid == right.ItemUid;
        }

        internal GameResult _LevelUp()
        {
            if (mTable == null || mLevelTable == null)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"AbilityItemHero._LevelUp: hero is not initialized. item_id={ItemId}");
            }

            var nextLevelTable = AbilityItemFactory.ResolveNextHeroLevelTable(ItemId, mLevelTable.item_level);
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
