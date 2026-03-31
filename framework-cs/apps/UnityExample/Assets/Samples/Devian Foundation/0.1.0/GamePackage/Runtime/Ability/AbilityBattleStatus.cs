using System;
using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityBattleStatus : AbilityBattleBase
    {
        STATUS mTable = null;

        public string StatusId => mTable?.StatusId ?? string.Empty;
        public string NameId => mTable?.NameId ?? string.Empty;
        public IReadOnlyList<string> AffectList => mTable != null ? mTable.AffectList : Array.Empty<string>();

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
