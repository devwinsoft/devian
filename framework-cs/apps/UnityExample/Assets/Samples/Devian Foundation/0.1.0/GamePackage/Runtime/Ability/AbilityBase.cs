using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public abstract class AbilityBase
    {
        Dictionary<STAT_TYPE, int> mStats = new();
        public IReadOnlyDictionary<STAT_TYPE, int> Stats => mStats;
        public int AtkPhysical => getScaledStat(
            STAT_TYPE.AFFECT_ATK_PHY_ADD,
            STAT_TYPE.AFFECT_ATK_PHY_PER,
            STAT_TYPE.ITEM_ATK_PHY,
            STAT_TYPE.UNIT_ATK_PHY);
        public int AtkMagical => getScaledStat(
            STAT_TYPE.AFFECT_ATK_MAG_ADD,
            STAT_TYPE.AFFECT_ATK_MAG_PER,
            STAT_TYPE.ITEM_ATK_MAG,
            STAT_TYPE.UNIT_ATK_MAG);
        public int DefPhysical => getScaledStat(
            STAT_TYPE.AFFECT_DEF_PHY_ADD,
            STAT_TYPE.AFFECT_DEF_PHY_PER,
            STAT_TYPE.ITEM_DEF_PHY,
            STAT_TYPE.UNIT_DEF_PHY);
        public int DefMagical => getScaledStat(
            STAT_TYPE.AFFECT_DEF_MAG_ADD,
            STAT_TYPE.AFFECT_DEF_MAG_PER,
            STAT_TYPE.ITEM_DEF_MAG,
            STAT_TYPE.UNIT_DEF_MAG);
        public int MaxHP => getScaledStat(
            STAT_TYPE.AFFECT_HP_ADD,
            STAT_TYPE.AFFECT_HP_PER,
            STAT_TYPE.ITEM_HP,
            STAT_TYPE.UNIT_HP);

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

        public IReadOnlyDictionary<STAT_TYPE, int> GetStats() => Stats;

        public abstract AbilityBase Clone();

        protected void CopyStatsFrom(AbilityBase source)
        {
            foreach (var kv in source.mStats)
                mStats[kv.Key] = kv.Value;
        }

        int getScaledStat(
            STAT_TYPE affectAddType,
            STAT_TYPE affectPerType,
            STAT_TYPE itemAddType,
            STAT_TYPE unitAddType)
        {
            var affectItemAdd = this[affectAddType] + this[itemAddType];
            var scaled = (int)(affectItemAdd * (100 + this[affectPerType]) * 0.01f);
            return scaled + this[unitAddType];
        }
    }
}
