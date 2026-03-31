using System;
using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityBattleProjectile : AbilityBattleBase
    {
        PROJECTILE mTable = null;

        public string ProjectileId => mTable?.ProjectileId ?? string.Empty;
        public string NameId => mTable?.NameId ?? string.Empty;
        public IReadOnlyList<string> AffectList => mTable != null ? mTable.AffectList : Array.Empty<string>();

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
