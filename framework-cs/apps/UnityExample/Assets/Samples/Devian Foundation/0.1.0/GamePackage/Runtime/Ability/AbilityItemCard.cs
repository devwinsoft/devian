using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityItemCard : AbilityItemBase
    {
        ITEM_CARD mTable = null;
        ITEM_CARD_LEVEL mLevelTable = null;

        public override string ItemId => mTable?.item_id ?? string.Empty;

        public void Init(ITEM_CARD table, ITEM_CARD_LEVEL levelTable)
        {
            mTable = table;
            mLevelTable = levelTable;

            if (levelTable == null)
                return;

            InitLevelStats(
                levelTable.item_level,
                levelTable.stat_type00, levelTable.stat_value00,
                levelTable.stat_type01, levelTable.stat_value01,
                levelTable.stat_type02, levelTable.stat_value02,
                levelTable.stat_type03, levelTable.stat_value03);
        }

        public override AbilityBase Clone()
        {
            var c = new AbilityItemCard();
            c.mTable = mTable;
            c.mLevelTable = mLevelTable;
            c.CopyStatsFrom(this);
            return c;
        }
    }
}
