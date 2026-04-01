using System;
using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityBattleSkill : AbilityBattleBase
    {
        SKILL mTable = null;

        public string SkillId => mTable?.skill_id ?? string.Empty;
        public string NameId => mTable?.name_id ?? string.Empty;
        public IReadOnlyList<string> AffectList => mTable != null ? mTable.affect_list : Array.Empty<string>();

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
