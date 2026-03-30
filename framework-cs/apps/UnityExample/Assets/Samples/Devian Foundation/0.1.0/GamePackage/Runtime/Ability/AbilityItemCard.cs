using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityItemCard : AbilityItemBase
    {
        ITEM_CARD mTable = null;

        public override string ItemId => mTable?.ItemId ?? string.Empty;

        public void Init(ITEM_CARD table)
        {
            mTable = table;
        }

        public override AbilityBase Clone()
        {
            var c = new AbilityItemCard();
            c.mTable = mTable;
            c.CopyStatsFrom(this);
            return c;
        }
    }
}
