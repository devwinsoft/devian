using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public abstract class BaseAbility
    {
        Dictionary<StatType, int> mStats = new();

        public int this[StatType type]
        {
            get => mStats.TryGetValue(type, out var v) ? v : 0;
        }

        public void AddStat(StatType type, int value)
        {
            mStats.TryGetValue(type, out var cur);
            mStats[type] = cur + value;
        }

        public void AddStat(BaseAbility other)
        {
            foreach (var kv in other.mStats)
                AddStat(kv.Key, kv.Value);
        }

        public void ClearStats() => mStats.Clear();
    }
}
