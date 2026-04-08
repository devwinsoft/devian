using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public abstract class AbilityBase
    {
        Dictionary<UNIT_STAT_TYPE, int> mStats = new();
        public IReadOnlyDictionary<UNIT_STAT_TYPE, int> Stats => mStats;
        public int AtkPhysical => getScaledStat(
            UNIT_STAT_TYPE.AFFECT_ATK_PHY_ADD,
            UNIT_STAT_TYPE.AFFECT_ATK_PHY_PER,
            UNIT_STAT_TYPE.ITEM_ATK_PHY,
            UNIT_STAT_TYPE.UNIT_ATK_PHY);
        public int AtkMagical => getScaledStat(
            UNIT_STAT_TYPE.AFFECT_ATK_MAG_ADD,
            UNIT_STAT_TYPE.AFFECT_ATK_MAG_PER,
            UNIT_STAT_TYPE.ITEM_ATK_MAG,
            UNIT_STAT_TYPE.UNIT_ATK_MAG);
        public int DefPhysical => getScaledStat(
            UNIT_STAT_TYPE.AFFECT_DEF_PHY_ADD,
            UNIT_STAT_TYPE.AFFECT_DEF_PHY_PER,
            UNIT_STAT_TYPE.ITEM_DEF_PHY,
            UNIT_STAT_TYPE.UNIT_DEF_PHY);
        public int DefMagical => getScaledStat(
            UNIT_STAT_TYPE.AFFECT_DEF_MAG_ADD,
            UNIT_STAT_TYPE.AFFECT_DEF_MAG_PER,
            UNIT_STAT_TYPE.ITEM_DEF_MAG,
            UNIT_STAT_TYPE.UNIT_DEF_MAG);
        public int MaxHP => getScaledStat(
            UNIT_STAT_TYPE.AFFECT_HP_ADD,
            UNIT_STAT_TYPE.AFFECT_HP_PER,
            UNIT_STAT_TYPE.ITEM_HP,
            UNIT_STAT_TYPE.UNIT_HP);

        public int this[UNIT_STAT_TYPE type]
        {
            get => mStats.TryGetValue(type, out var v) ? v : 0;
        }

        internal void AddStat(UNIT_STAT_TYPE type, int value)
        {
            mStats.TryGetValue(type, out var cur);
            mStats[type] = cur + value;
        }

        internal void AddStat(AbilityBase other)
        {
            foreach (var kv in other.mStats)
                AddStat(kv.Key, kv.Value);
        }

        public int GetInt(UNIT_STAT_TYPE type) => mStats.TryGetValue(type, out var v) ? v : 0;

        public float GetFloat(UNIT_STAT_TYPE type) => GetInt(type) * 0.0001f;

        internal void SetStat(UNIT_STAT_TYPE type, int value) => mStats[type] = value;

        internal void ClearStat(UNIT_STAT_TYPE type) => mStats.Remove(type);

        internal void ClearStats() => mStats.Clear();

        public IReadOnlyDictionary<UNIT_STAT_TYPE, int> GetStats() => Stats;

        public abstract AbilityBase Clone();

        protected void CopyStatsFrom(AbilityBase source)
        {
            foreach (var kv in source.mStats)
                mStats[kv.Key] = kv.Value;
        }

        int getScaledStat(
            UNIT_STAT_TYPE affectAddType,
            UNIT_STAT_TYPE affectPerType,
            UNIT_STAT_TYPE itemAddType,
            UNIT_STAT_TYPE unitAddType)
        {
            var affectItemAdd = this[affectAddType] + this[itemAddType];
            var scaled = (int)(affectItemAdd * (100 + this[affectPerType]) * 0.01f);
            return scaled + this[unitAddType];
        }
    }
}
