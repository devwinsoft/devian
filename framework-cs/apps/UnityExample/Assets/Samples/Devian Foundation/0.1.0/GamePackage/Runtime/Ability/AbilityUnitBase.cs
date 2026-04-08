using Devian.Domain.Game;

namespace Devian
{
    public abstract class AbilityUnitBase : AbilityBase
    {
        public abstract string UnitId { get; }
        int mCurHP = 0;

        public int UnitLevel => this[UNIT_STAT_TYPE.UNIT_LEVEL];
        public int CurHP => mCurHP;

        protected void InitUnitState(int unitLevel, int maxHp)
        {
            SetStat(UNIT_STAT_TYPE.UNIT_LEVEL, unitLevel);
            SetStat(UNIT_STAT_TYPE.UNIT_HP, maxHp);
            mCurHP = MaxHP;
        }

        protected void CopyUnitStateFrom(AbilityUnitBase source)
        {
            mCurHP = source.mCurHP;
        }

        internal void ResetCurrentHpToMax()
        {
            mCurHP = MaxHP;
        }
    }
}
