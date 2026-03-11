using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public abstract class AbilityBase
    {
        Dictionary<STAT_TYPE, int> mStats = new();

        public int this[STAT_TYPE type]
        {
            get => mStats.TryGetValue(type, out var v) ? v : 0;
        }

        public void AddStat(STAT_TYPE type, int value)
        {
            mStats.TryGetValue(type, out var cur);
            mStats[type] = cur + value;
        }

        public void AddStat(AbilityBase other)
        {
            foreach (var kv in other.mStats)
                AddStat(kv.Key, kv.Value);
        }

        public int GetInt(STAT_TYPE type) => mStats.TryGetValue(type, out var v) ? v : 0;

        public float GetFloat(STAT_TYPE type) => GetInt(type) * 0.0001f;

        public void SetStat(STAT_TYPE type, int value) => mStats[type] = value;

        public void ClearStat(STAT_TYPE type) => mStats.Remove(type);

        public void ClearStats() => mStats.Clear();

        public IReadOnlyDictionary<STAT_TYPE, int> GetStats() => mStats;

        public abstract AbilityBase Clone();

        protected void CopyStatsFrom(AbilityBase source)
        {
            foreach (var kv in source.mStats)
                mStats[kv.Key] = kv.Value;
        }
    }
}
