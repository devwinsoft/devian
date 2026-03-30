using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    internal static class AbilityFactoryStatUtil
    {
        public static void ApplyStats(AbilityBase ability, IReadOnlyDictionary<STAT_TYPE, int> stats)
        {
            if (ability == null || stats == null)
                return;

            foreach (var kv in stats)
                ability.SetStat(kv.Key, kv.Value);
        }
    }
}
