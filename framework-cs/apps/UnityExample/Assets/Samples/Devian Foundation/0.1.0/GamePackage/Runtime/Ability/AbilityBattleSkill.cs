using System;
using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityBattleSkill : AbilityBattleBase
    {
        SKILL mTable = null;

        public string SkillId => mTable?.SkillId ?? string.Empty;
        public string NameId => mTable?.NameId ?? string.Empty;
        public IReadOnlyList<string> AffectList => mTable != null ? mTable.AffectList : Array.Empty<string>();

        public void Init(SKILL table)
        {
            mTable = table;
        }

        public override AbilityBase Clone()
        {
            var c = new AbilityBattleSkill();
            c.mTable = mTable;
            c.CopyStatsFrom(this);
            return c;
        }
    }
}
