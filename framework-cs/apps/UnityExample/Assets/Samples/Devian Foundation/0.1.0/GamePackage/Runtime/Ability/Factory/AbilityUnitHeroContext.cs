using System.Collections.Generic;

namespace Devian
{
    public sealed class AbilityUnitHeroContext
    {
        public string UnitId { get; set; }
        public int UnitLevel { get; set; } = 1;
        public IReadOnlyDictionary<int, AbilityItemEquip> Equips { get; set; }
    }
}
