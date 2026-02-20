using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityCard : AbilityBase
    {
        CARD mTable = null;

        public string CardId => mTable?.CardId ?? string.Empty;
        public int Amount => this[StatType.CardAmount];

        public void Init(CARD table)
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
            AddStat(StatType.CardAmount, delta);
        }
    }
}
