using Devian.Domain.Game;
using System.Collections.Generic;

namespace Devian
{
    public sealed class AbilityUnitHeroContext
    {
        public string UnitId { get; set; }
        public int UnitLevel { get; set; } = 1;
        public IReadOnlyDictionary<SLOT_TYPE, AbilityItemEquip> Equips { get; set; }
    }
}
