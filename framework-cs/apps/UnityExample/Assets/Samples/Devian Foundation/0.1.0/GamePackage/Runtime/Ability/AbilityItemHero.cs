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
        public string UnitId => mTable?.UnitId ?? string.Empty;
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

        public bool SetEquip(AbilityItemEquip equip, int slotNumber)
        {
            if (equip == null || slotNumber <= 0)
                return false;

            if (mEquips.TryGetValue(slotNumber, out var prev))
            {
                if (isSameEquip(prev, equip))
                {
                    prev.SetOwner(HeroId, slotNumber);
                    return true;
                }

                if (prev.OwnerUnitId == HeroId && prev.OwnerSlotNumber == slotNumber)
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
            equip.SetOwner(HeroId, slotNumber);
            return true;
        }

        public bool RemoveEquip(int slotNumber)
        {
            if (!mEquips.TryGetValue(slotNumber, out var equip))
                return false;

            if (equip.OwnerUnitId == HeroId && equip.OwnerSlotNumber == slotNumber)
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

    }
}
