using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityItemCard : AbilityItemBase
    {
        ITEM_CARD mTable = null;
        ITEM_CARD_LEVEL mLevelTable = null;

        public override string ItemId => mTable?.ItemId ?? string.Empty;

        public void Init(ITEM_CARD table, ITEM_CARD_LEVEL levelTable)
        {
            mTable = table;
            mLevelTable = levelTable;

            if (levelTable == null)
                return;

            InitLevelStats(
                levelTable.ItemLevel,
                levelTable.StatType00, levelTable.StatValue00,
                levelTable.StatType01, levelTable.StatValue01,
                levelTable.StatType02, levelTable.StatValue02,
                levelTable.StatType03, levelTable.StatValue03);
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
