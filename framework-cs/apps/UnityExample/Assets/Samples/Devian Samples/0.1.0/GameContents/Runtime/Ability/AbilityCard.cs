using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityCard : AbilityBase
    {
        ITEM_CARD mTable = null;

        public string CardId => mTable?.CardId ?? string.Empty;
        public int Amount => this[STAT_TYPE.CARD_AMOUNT];

        public void Init(ITEM_CARD table)
        {
            mTable = table;
        }

        public override AbilityBase Clone()
        {
            var c = new AbilityCard();
            c.mTable = mTable;
            c.CopyStatsFrom(this);
            return c;
        }

        public void AddAmount(int delta)
        {
            AddStat(STAT_TYPE.CARD_AMOUNT, delta);
        }
    }
}
