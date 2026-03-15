using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class TreasureStorageCurrent
    {
        public int Exp { get; set; }
        public int Level { get; set; } = 1;

        public void Reset(int level = 1, int exp = 0)
        {
            Level = level < 1 ? 1 : level;
            Exp = exp < 0 ? 0 : exp;
        }

        public void Clear()
        {
            Exp = 0;
            Level = 1;
        }
    }

    public sealed class TreasureStorage
    {
        public int SchemaVersion { get; set; } = 1;
        public Dictionary<TREASURE_GRADE_TYPE, int> ChestCounts { get; } = new();
        public TreasureStorageCurrent Current { get; } = new();

        public int GetChestCount(TREASURE_GRADE_TYPE gradeType)
        {
            return ChestCounts.TryGetValue(gradeType, out var count) ? count : 0;
        }

        public void AddChest(TREASURE_GRADE_TYPE gradeType, int amount)
        {
            if (amount <= 0)
                return;

            ChestCounts.TryGetValue(gradeType, out var current);
            ChestCounts[gradeType] = current + amount;
        }

        public void SetChestCount(TREASURE_GRADE_TYPE gradeType, int count)
        {
            if (count < 0)
                count = 0;

            ChestCounts[gradeType] = count;
        }

        public void AddCurrentExp(int amount)
        {
            if (amount <= 0)
                return;

            Current.Exp += amount;
        }

        public void ResetCurrent(int level = 1, int exp = 0)
        {
            Current.Reset(level, exp);
        }

        public void Clear()
        {
            SchemaVersion = 1;
            ChestCounts.Clear();
            Current.Clear();
        }
    }
}
