using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityItemHero : AbilityItemBase
    {
        ITEM_HERO mTable = null;
        ITEM_HERO_LEVEL mLevelTable = null;
        readonly Dictionary<int, AbilityItemEquip> mEquips = new();

        public string HeroId => mTable?.ItemId ?? string.Empty;
        public override string ItemId => mTable?.ItemId ?? string.Empty;
        public IReadOnlyDictionary<int, AbilityItemEquip> Equips => mEquips;

        public void Init(ITEM_HERO table, ITEM_HERO_LEVEL levelTable)
        {
            mTable = table;
            mLevelTable = levelTable;

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
            var c = new AbilityItemHero();
            c.mTable = mTable;
            c.mLevelTable = mLevelTable;
            c.CopyStatsFrom(this);
            foreach (var kv in mEquips)
                c.mEquips[kv.Key] = kv.Value;
            return c;
        }

        public bool Equip(AbilityItemEquip equip, int slotNumber)
        {
            if (equip == null || slotNumber <= 0)
                return false;

            if (equip.IsEquipped)
            {
                if (equip.OwnerUnitId != HeroId)
                    return false;

                if (mEquips.TryGetValue(equip.OwnerSlotNumber, out var current)
                    && isSameEquip(current, equip))
                {
                    if (equip.OwnerSlotNumber == slotNumber)
                        return true;

                    if (!Unequip(equip.OwnerSlotNumber))
                        return false;
                }
                else
                {
                    // Recover local owner metadata that is not backed by this hero slot map.
                    equip.ClearOwner();
                }
            }

            if (mEquips.TryGetValue(slotNumber, out var prev))
            {
                if (isSameEquip(prev, equip))
                {
                    prev.SetOwner(HeroId, slotNumber);
                    return true;
                }

                if (!Unequip(slotNumber))
                    return false;
            }

            var existingSlot = findEquipSlot(equip);
            if (existingSlot > 0)
            {
                if (existingSlot == slotNumber)
                    return true;

                if (!Unequip(existingSlot))
                    return false;
            }

            mEquips[slotNumber] = equip;
            equip.SetOwner(HeroId, slotNumber);
            applyEquipStats(equip, +1);
            return true;
        }

        public bool Unequip(int slotNumber)
        {
            if (!mEquips.TryGetValue(slotNumber, out var equip))
                return false;

            applyEquipStats(equip, -1);

            if (equip.OwnerUnitId == HeroId && equip.OwnerSlotNumber == slotNumber)
                equip.ClearOwner();

            mEquips.Remove(slotNumber);
            return true;
        }

        void applyEquipStats(AbilityItemEquip equip, int sign)
        {
            if (equip == null || sign == 0)
                return;

            foreach (var kv in equip.GetStats())
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
