using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public abstract class AbilityBase
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

        public void AddStat(AbilityBase other)
        {
            foreach (var kv in other.mStats)
                AddStat(kv.Key, kv.Value);
        }

        public int GetInt(StatType type) => mStats.TryGetValue(type, out var v) ? v : 0;

        public float GetFloat(StatType type) => GetInt(type) * 0.0001f;

        public void SetStat(StatType type, int value) => mStats[type] = value;

        public void ClearStat(StatType type) => mStats.Remove(type);

        public void ClearStats() => mStats.Clear();

        public IReadOnlyDictionary<StatType, int> GetStats() => mStats;

        public abstract AbilityBase Clone();

        protected void CopyStatsFrom(AbilityBase source)
        {
            foreach (var kv in source.mStats)
                mStats[kv.Key] = kv.Value;
        }
    }
}
