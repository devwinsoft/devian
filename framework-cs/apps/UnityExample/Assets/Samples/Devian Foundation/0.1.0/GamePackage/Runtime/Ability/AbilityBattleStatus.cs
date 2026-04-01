using System;
using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityBattleStatus : AbilityBattleBase
    {
        STATUS mTable = null;

        public string StatusId => mTable?.status_id ?? string.Empty;
        public string NameId => mTable?.name_id ?? string.Empty;
        public IReadOnlyList<string> AffectList => mTable != null ? mTable.affect_list : Array.Empty<string>();

        public void Init(STATUS table)
        {
            mTable = table;
        }

        public override AbilityBase Clone()
        {
            var c = new AbilityBattleStatus();
            c.mTable = mTable;
            c.CopyStatsFrom(this);
            return c;
        }
    }
}
