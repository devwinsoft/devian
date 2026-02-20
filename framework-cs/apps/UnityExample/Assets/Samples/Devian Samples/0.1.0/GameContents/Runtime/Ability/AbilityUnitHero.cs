using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityUnitHero : AbilityUnitBase
    {
        UNIT_HERO mTable = null;
        readonly Dictionary<int, AbilityEquip> mEquips = new();

        public override string UnitId => mTable?.UnitId ?? string.Empty;
        public IReadOnlyDictionary<int, AbilityEquip> Equips => mEquips;

        public void Init(UNIT_HERO table)
        {
            mTable = table;
            AddStat(StatType.UnitHpMax, table.MaxHp);
        }

        public override AbilityBase Clone()
        {
            var c = new AbilityUnitHero();
            c.mTable = mTable;
            c.CopyStatsFrom(this);
            return c;
        }

        public bool Equip(AbilityEquip equip, int slotNumber)
        {
            if (equip == null || slotNumber <= 0) return false;

            if (equip.IsEquipped) equip.ClearOwner();

            if (mEquips.TryGetValue(slotNumber, out var prev))
                prev.ClearOwner();

            mEquips[slotNumber] = equip;
            equip.SetOwner(UnitId, slotNumber);
            return true;
        }

        public bool Unequip(int slotNumber)
        {
            if (!mEquips.TryGetValue(slotNumber, out var equip)) return false;
            equip.ClearOwner();
            mEquips.Remove(slotNumber);
            return true;
        }
    }
}
