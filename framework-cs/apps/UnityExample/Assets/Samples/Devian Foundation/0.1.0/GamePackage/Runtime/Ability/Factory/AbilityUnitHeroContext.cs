using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class AbilityUnitHeroContext
    {
        public string UnitId { get; set; }
        public AbilityItemHero SourceItemHero { get; set; }
        public IReadOnlyDictionary<int, AbilityItemEquip> SourceEquips { get; set; }
        public IReadOnlyDictionary<STAT_TYPE, int> OverrideStats { get; set; }
        public bool CopyItemLevel { get; set; }
        public bool CloneEquips { get; set; } = true;
    }
}
