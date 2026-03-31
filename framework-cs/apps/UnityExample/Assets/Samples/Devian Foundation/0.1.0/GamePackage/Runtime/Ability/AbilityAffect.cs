using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityAffect : AbilityBase
    {
        AFFECT mTable = null;

        public string AffectId => mTable?.AffectId ?? string.Empty;
        public string NameId => mTable?.NameId ?? string.Empty;

        public void Init(AFFECT table)
        {
            mTable = table;
        }

        public override AbilityBase Clone()
        {
            var c = new AbilityAffect();
            c.mTable = mTable;
            c.CopyStatsFrom(this);
            return c;
        }
    }
}
