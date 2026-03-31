using Devian.Domain.Game;

namespace Devian
{
    public abstract class AbilityItemBase : AbilityBase
    {
        public abstract string ItemId { get; }
        public int Amount => this[STAT_TYPE.ITEM_AMOUNT];
        public int ItemLevel => this[STAT_TYPE.ITEM_LEVEL];

        public void AddAmount(int delta)
        {
            AddStat(STAT_TYPE.ITEM_AMOUNT, delta);
        }

        protected void InitLevelStats(
            int itemLevel,
            STAT_TYPE statType00, int statValue00,
            STAT_TYPE statType01, int statValue01,
            STAT_TYPE statType02, int statValue02,
            STAT_TYPE statType03, int statValue03)
        {
            SetStat(STAT_TYPE.ITEM_LEVEL, itemLevel);
            applyLevelStat(statType00, statValue00);
            applyLevelStat(statType01, statValue01);
            applyLevelStat(statType02, statValue02);
            applyLevelStat(statType03, statValue03);
        }

        void applyLevelStat(STAT_TYPE statType, int statValue)
        {
            if (statType == STAT_TYPE.NONE)
                return;

            SetStat(statType, statValue);
        }
    }
}
