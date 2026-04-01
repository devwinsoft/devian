using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityUnitMonster : AbilityUnitBase
    {
        UNIT_MONSTER mTable = null;
        UNIT_MONSTER_LEVEL mLevelTable = null;

        public override string UnitId => mTable?.unit_id ?? string.Empty;

        public void Init(UNIT_MONSTER table, UNIT_MONSTER_LEVEL levelTable)
        {
            mTable = table;
            mLevelTable = levelTable;

            if (levelTable == null)
                return;

            InitUnitState(levelTable.unit_level, levelTable.max_hp);
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
