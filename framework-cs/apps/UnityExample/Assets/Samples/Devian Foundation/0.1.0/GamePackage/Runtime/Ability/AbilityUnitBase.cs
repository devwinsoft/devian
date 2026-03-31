using Devian.Domain.Game;

namespace Devian
{
    public abstract class AbilityUnitBase : AbilityBase
    {
        public abstract string UnitId { get; }

        public int UnitLevel => this[STAT_TYPE.UNIT_LEVEL];
        public int CurHP => mCurHP;
        protected int mCurHP = 0;

        protected void InitUnitState(int unitLevel, int maxHp)
        {
            SetStat(STAT_TYPE.UNIT_LEVEL, unitLevel);
            SetStat(STAT_TYPE.UNIT_HP, maxHp);
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
