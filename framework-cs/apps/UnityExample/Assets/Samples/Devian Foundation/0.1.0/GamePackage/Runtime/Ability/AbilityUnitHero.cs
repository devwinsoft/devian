using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityUnitHero : AbilityUnitBase
    {
        UNIT_HERO mTable = null;
        readonly Dictionary<int, AbilityItemEquip> mEquips = new();

        public override string UnitId => mTable?.UnitId ?? string.Empty;
        public IReadOnlyDictionary<int, AbilityItemEquip> Equips => mEquips;

        public void Init(UNIT_HERO table)
        {
            mTable = table;
            AddStat(STAT_TYPE.UNIT_HP_MAX, table.MaxHp);
        }

        public override AbilityBase Clone()
        {
            var c = new AbilityUnitHero();
            c.mTable = mTable;
            c.CopyStatsFrom(this);
            foreach (var kv in mEquips)
                c.mEquips[kv.Key] = kv.Value;
            return c;
        }

        internal void ClearProjectedEquips()
        {
            foreach (var kv in mEquips)
            {
                if (kv.Value != null && kv.Value.OwnerUnitId == UnitId && kv.Value.OwnerSlotNumber == kv.Key)
                    kv.Value.ClearOwner();
            }

            mEquips.Clear();
        }

        internal bool SetProjectedEquip(AbilityItemEquip equip, int slotNumber)
        {
            if (equip == null || slotNumber <= 0)
                return false;

            if (equip.IsEquipped)
                equip.ClearOwner();

            if (mEquips.TryGetValue(slotNumber, out var prev))
                prev.ClearOwner();

            mEquips[slotNumber] = equip;
            equip.SetOwner(UnitId, slotNumber);
            return true;
        }
    }
}
