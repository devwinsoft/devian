using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityUnitHero : AbilityUnitBase
    {
        UNIT_HERO mTable = null;
        UNIT_HERO_LEVEL mLevelTable = null;
        readonly Dictionary<SLOT_TYPE, AbilityItemEquip> mEquips = new();

        public override string UnitId => mTable?.unit_id ?? string.Empty;
        public IReadOnlyDictionary<SLOT_TYPE, AbilityItemEquip> Equips => mEquips;

        public void Init(UNIT_HERO table, UNIT_HERO_LEVEL levelTable)
        {
            mTable = table;
            mLevelTable = levelTable;

            if (levelTable == null)
                return;

            InitUnitState(levelTable.unit_level, levelTable.max_hp);
        }

        public override AbilityBase Clone()
        {
            var c = new AbilityUnitHero();
            c.mTable = mTable;
            c.mLevelTable = mLevelTable;
            c.CopyStatsFrom(this);
            c.CopyUnitStateFrom(this);
            foreach (var kv in mEquips)
                c.mEquips[kv.Key] = kv.Value;
            return c;
        }

        internal bool _Equip(AbilityItemEquip equip, SLOT_TYPE slotType)
        {
            if (equip == null || slotType == SLOT_TYPE.NONE)
                return false;

            if (AbilityEquipSlotPolicy.GetPlacementFailure(equip, slotType, mEquips) != AbilityEquipPlacementFailure.None)
                return false;

            if (slotType == SLOT_TYPE.HAND_MAIN && AbilityEquipSlotPolicy.IsTwoHanded(equip))
                _Unequip(SLOT_TYPE.HAND_SUB);

            if (mEquips.TryGetValue(slotType, out var prev))
            {
                if (AbilityItemEquip.IsSame(prev, equip))
                {
                    prev.SetOwner(UnitId, slotType);
                    return true;
                }

                _Unequip(slotType);
            }

            var existingSlot = findEquipSlot(equip);
            if (existingSlot != SLOT_TYPE.NONE)
            {
                if (existingSlot == slotType)
                    return true;

                _Unequip(existingSlot);
            }

            if (equip.IsEquipped)
                equip.ClearOwner();

            mEquips[slotType] = equip;
            equip.SetOwner(UnitId, slotType);
            applyEquipStats(equip, +1);
            return true;
        }

        internal bool _Unequip(SLOT_TYPE slotType)
        {
            if (!mEquips.TryGetValue(slotType, out var equip))
                return false;

            applyEquipStats(equip, -1);

            if (equip.OwnerUnitId == UnitId && equip.OwnerSlotType == slotType)
                equip.ClearOwner();

            mEquips.Remove(slotType);
            return true;
        }

        void applyEquipStats(AbilityItemEquip equip, int sign)
        {
            if (equip == null || sign == 0)
                return;

            foreach (var kv in equip.Stats)
            {
                if (!shouldApplyEquipStat(kv.Key))
                    continue;

                AddStat(kv.Key, kv.Value * sign);
            }
        }

        SLOT_TYPE findEquipSlot(AbilityItemEquip equip)
        {
            foreach (var kv in mEquips)
            {
                if (AbilityItemEquip.IsSame(kv.Value, equip))
                    return kv.Key;
            }

            return SLOT_TYPE.NONE;
        }

        static bool shouldApplyEquipStat(STAT_TYPE statType)
        {
            return statType != STAT_TYPE.NONE
                && statType != STAT_TYPE.ITEM_LEVEL
                && statType != STAT_TYPE.ITEM_AMOUNT;
        }
    }
}
