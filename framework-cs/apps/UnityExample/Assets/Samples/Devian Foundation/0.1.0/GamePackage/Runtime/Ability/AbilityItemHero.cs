using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityItemHero : AbilityItemBase
    {
        ITEM_HERO mTable = null;
        ITEM_HERO_LEVEL mLevelTable = null;
        readonly Dictionary<EQUIP_SLOT_TYPE, AbilityItemEquip> mEquips = new();

        public string UnitId => mTable?.unit_id ?? string.Empty;
        public override string ItemId => mTable?.item_id ?? string.Empty;
        public IReadOnlyDictionary<EQUIP_SLOT_TYPE, AbilityItemEquip> Equips => mEquips;

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

        public AbilityItemEquip GetEquip(EQUIP_SLOT_TYPE slotType)
        {
            return mEquips.TryGetValue(slotType, out var equip) ? equip : null;
        }

        internal bool _SetEquip(AbilityItemEquip equip, EQUIP_SLOT_TYPE slotType)
        {
            if (equip == null || slotType == EQUIP_SLOT_TYPE.NONE)
                return false;

            if (AbilityEquipSlotPolicy.GetPlacementFailure(equip, slotType, mEquips) != AbilityEquipPlacementFailure.None)
                return false;

            if (slotType == EQUIP_SLOT_TYPE.HAND_MAIN && AbilityEquipSlotPolicy.IsTwoHanded(equip))
                _RemoveEquip(EQUIP_SLOT_TYPE.HAND_SUB);

            if (mEquips.TryGetValue(slotType, out var prev))
            {
                if (AbilityItemEquip.IsSame(prev, equip))
                {
                    prev.SetOwner(ItemId, slotType);
                    return true;
                }

                if (prev.OwnerUnitId == ItemId && prev.OwnerSlotType == slotType)
                    prev.ClearOwner();
            }

            var existingSlot = findEquipSlot(equip);
            if (existingSlot != EQUIP_SLOT_TYPE.NONE)
            {
                if (existingSlot == slotType)
                    return true;

                mEquips.Remove(existingSlot);
            }

            mEquips[slotType] = equip;
            equip.SetOwner(ItemId, slotType);
            return true;
        }

        internal bool _RemoveEquip(EQUIP_SLOT_TYPE slotType)
        {
            if (!mEquips.TryGetValue(slotType, out var equip))
                return false;

            if (equip.OwnerUnitId == ItemId && equip.OwnerSlotType == slotType)
                equip.ClearOwner();

            mEquips.Remove(slotType);
            return true;
        }

        EQUIP_SLOT_TYPE findEquipSlot(AbilityItemEquip equip)
        {
            foreach (var kv in mEquips)
            {
                if (AbilityItemEquip.IsSame(kv.Value, equip))
                    return kv.Key;
            }

            return EQUIP_SLOT_TYPE.NONE;
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
