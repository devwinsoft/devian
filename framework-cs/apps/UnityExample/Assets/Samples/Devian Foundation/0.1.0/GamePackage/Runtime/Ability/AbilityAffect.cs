using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityAffect : AbilityBase
    {
        AFFECT mTable = null;

        public string AffectId => mTable?.affect_id ?? string.Empty;
        public string NameId => mTable?.name_id ?? string.Empty;

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
