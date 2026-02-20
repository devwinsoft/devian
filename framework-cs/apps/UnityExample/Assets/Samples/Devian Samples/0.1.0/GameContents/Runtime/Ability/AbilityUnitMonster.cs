using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityUnitMonster : AbilityUnitBase
    {
        UNIT_MONSTER mTable = null;

        public override string UnitId => mTable?.UnitId ?? string.Empty;

        public void Init(UNIT_MONSTER table)
        {
            mTable = table;
            AddStat(STAT_TYPE.UNIT_HP_MAX, table.MaxHp);
        }

        public override AbilityBase Clone()
        {
            var c = new AbilityUnitMonster();
            c.mTable = mTable;
            c.CopyStatsFrom(this);
            return c;
        }
    }
}
