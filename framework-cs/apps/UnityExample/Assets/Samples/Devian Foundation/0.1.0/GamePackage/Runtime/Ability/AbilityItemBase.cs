using Devian.Domain.Game;

namespace Devian
{
    public abstract class AbilityItemBase : AbilityBase
    {
        public abstract string ItemId { get; }
        public int Amount => this[STAT_TYPE.ITEM_AMOUNT];
        public int ItemLevel => this[STAT_TYPE.ITEM_LEVEL];

        internal void AddAmount(int delta)
        {
            AddStat(STAT_TYPE.ITEM_AMOUNT, delta);
        }

        protected void ReplaceLevelStats(
            int nextItemLevel,
            STAT_TYPE currentStatType00, int currentStatValue00,
            STAT_TYPE currentStatType01, int currentStatValue01,
            STAT_TYPE currentStatType02, int currentStatValue02,
            STAT_TYPE currentStatType03, int currentStatValue03,
            STAT_TYPE nextStatType00, int nextStatValue00,
            STAT_TYPE nextStatType01, int nextStatValue01,
            STAT_TYPE nextStatType02, int nextStatValue02,
            STAT_TYPE nextStatType03, int nextStatValue03)
        {
            removeLevelStats(
                currentStatType00, currentStatValue00,
                currentStatType01, currentStatValue01,
                currentStatType02, currentStatValue02,
                currentStatType03, currentStatValue03);

            applyLevelStats(
                nextItemLevel,
                nextStatType00, nextStatValue00,
                nextStatType01, nextStatValue01,
                nextStatType02, nextStatValue02,
                nextStatType03, nextStatValue03);
        }

        protected void InitLevelStats(
            int itemLevel,
            STAT_TYPE statType00, int statValue00,
            STAT_TYPE statType01, int statValue01,
            STAT_TYPE statType02, int statValue02,
            STAT_TYPE statType03, int statValue03)
        {
            applyLevelStats(
                itemLevel,
                statType00, statValue00,
                statType01, statValue01,
                statType02, statValue02,
                statType03, statValue03);
        }

        void removeLevelStats(
            STAT_TYPE statType00, int statValue00,
            STAT_TYPE statType01, int statValue01,
            STAT_TYPE statType02, int statValue02,
            STAT_TYPE statType03, int statValue03)
        {
            applyLevelStatDelta(statType00, -statValue00);
            applyLevelStatDelta(statType01, -statValue01);
            applyLevelStatDelta(statType02, -statValue02);
            applyLevelStatDelta(statType03, -statValue03);
        }

        void applyLevelStats(
            int itemLevel,
            STAT_TYPE statType00, int statValue00,
            STAT_TYPE statType01, int statValue01,
            STAT_TYPE statType02, int statValue02,
            STAT_TYPE statType03, int statValue03)
        {
            SetStat(STAT_TYPE.ITEM_LEVEL, itemLevel);
            applyLevelStatDelta(statType00, statValue00);
            applyLevelStatDelta(statType01, statValue01);
            applyLevelStatDelta(statType02, statValue02);
            applyLevelStatDelta(statType03, statValue03);
        }

        void applyLevelStatDelta(STAT_TYPE statType, int delta)
        {
            if (statType == STAT_TYPE.NONE
                || statType == STAT_TYPE.ITEM_LEVEL
                || statType == STAT_TYPE.ITEM_AMOUNT
                || delta == 0)
                return;

            AddStat(statType, delta);
        }
    }
}
