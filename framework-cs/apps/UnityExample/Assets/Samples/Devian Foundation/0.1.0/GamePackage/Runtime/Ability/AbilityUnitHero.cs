using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityUnitHero : AbilityUnitBase
    {
        UNIT_HERO mTable = null;
        UNIT_HERO_LEVEL mLevelTable = null;
        readonly Dictionary<int, AbilityItemEquip> mEquips = new();

        public override string UnitId => mTable?.unit_id ?? string.Empty;
        public IReadOnlyDictionary<int, AbilityItemEquip> Equips => mEquips;

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

        public bool Equip(AbilityItemEquip equip, int slotNumber)
        {
            if (equip == null || slotNumber <= 0)
                return false;

            if (mEquips.TryGetValue(slotNumber, out var prev))
            {
                if (isSameEquip(prev, equip))
                {
                    prev.SetOwner(UnitId, slotNumber);
                    return true;
                }

                Unequip(slotNumber);
            }

            var existingSlot = findEquipSlot(equip);
            if (existingSlot > 0)
            {
                if (existingSlot == slotNumber)
                    return true;

                Unequip(existingSlot);
            }

            if (equip.IsEquipped)
                equip.ClearOwner();

            mEquips[slotNumber] = equip;
            equip.SetOwner(UnitId, slotNumber);
            applyEquipStats(equip, +1);
            return true;
        }

        public bool Unequip(int slotNumber)
        {
            if (!mEquips.TryGetValue(slotNumber, out var equip))
                return false;

            applyEquipStats(equip, -1);

            if (equip.OwnerUnitId == UnitId && equip.OwnerSlotNumber == slotNumber)
                equip.ClearOwner();

            mEquips.Remove(slotNumber);
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

        static bool shouldApplyEquipStat(STAT_TYPE statType)
        {
            return statType != STAT_TYPE.NONE
                && statType != STAT_TYPE.ITEM_LEVEL
                && statType != STAT_TYPE.ITEM_AMOUNT;
        }
    }
}
