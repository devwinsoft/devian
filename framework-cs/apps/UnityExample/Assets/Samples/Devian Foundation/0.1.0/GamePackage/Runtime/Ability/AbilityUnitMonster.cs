using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityUnitMonster : AbilityUnitBase
    {
        UNIT_MONSTER mTable = null;
        UNIT_MONSTER_LEVEL mLevelTable = null;

        public override string UnitId => mTable?.UnitId ?? string.Empty;

        public void Init(UNIT_MONSTER table, UNIT_MONSTER_LEVEL levelTable)
        {
            mTable = table;
            mLevelTable = levelTable;

            if (levelTable == null)
                return;

            InitUnitState(levelTable.UnitLevel, levelTable.MaxHp);
        }

        public override AbilityBase Clone()
        {
            var c = new AbilityUnitMonster();
            c.mTable = mTable;
            c.mLevelTable = mLevelTable;
            c.CopyStatsFrom(this);
            c.CopyUnitStateFrom(this);
            return c;
        }
    }
}
