using System;
using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityBattleProjectile : AbilityBattleBase
    {
        PROJECTILE mTable = null;

        public string ProjectileId => mTable?.projectile_id ?? string.Empty;
        public string NameId => mTable?.name_id ?? string.Empty;
        public IReadOnlyList<string> AffectList => mTable != null ? mTable.affect_list : Array.Empty<string>();

        public void Init(PROJECTILE table)
        {
            mTable = table;
        }

        public override AbilityBase Clone()
        {
            var c = new AbilityBattleProjectile();
            c.mTable = mTable;
            c.CopyStatsFrom(this);
            return c;
        }
    }
}
