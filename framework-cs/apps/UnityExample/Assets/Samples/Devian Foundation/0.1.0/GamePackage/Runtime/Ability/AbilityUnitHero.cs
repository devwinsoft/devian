using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityUnitHero : AbilityUnitBase
    {
        UNIT_HERO mTable = null;
        UNIT_HERO_LEVEL mLevelTable = null;
        readonly Dictionary<EQUIP_SLOT_TYPE, AbilityItemEquip> mEquips = new();

        public override string UnitId => mTable?.unit_id ?? string.Empty;
        public IReadOnlyDictionary<EQUIP_SLOT_TYPE, AbilityItemEquip> Equips => mEquips;

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

        internal bool _Equip(AbilityItemEquip equip, EQUIP_SLOT_TYPE slotType)
        {
            if (equip == null || slotType == EQUIP_SLOT_TYPE.NONE)
                return false;

            if (AbilityEquipSlotPolicy.GetPlacementFailure(equip, slotType, mEquips) != AbilityEquipPlacementFailure.None)
                return false;

            if (slotType == EQUIP_SLOT_TYPE.HAND_MAIN && AbilityEquipSlotPolicy.IsTwoHanded(equip))
                _Unequip(EQUIP_SLOT_TYPE.HAND_SUB);

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
            if (existingSlot != EQUIP_SLOT_TYPE.NONE)
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

        internal bool _Unequip(EQUIP_SLOT_TYPE slotType)
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

        EQUIP_SLOT_TYPE findEquipSlot(AbilityItemEquip equip)
        {
            foreach (var kv in mEquips)
            {
                if (AbilityItemEquip.IsSame(kv.Value, equip))
                    return kv.Key;
            }

            return EQUIP_SLOT_TYPE.NONE;
        }

        static bool shouldApplyEquipStat(UNIT_STAT_TYPE statType)
        {
            return statType != UNIT_STAT_TYPE.NONE
                && statType != UNIT_STAT_TYPE.ITEM_LEVEL
                && statType != UNIT_STAT_TYPE.ITEM_AMOUNT;
        }
    }
}
