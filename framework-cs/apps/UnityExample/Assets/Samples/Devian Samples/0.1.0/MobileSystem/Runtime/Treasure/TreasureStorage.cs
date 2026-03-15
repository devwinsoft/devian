using System.Collections.Generic;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class TreasureStorageProgress
    {
        public int CurrentExp { get; set; }
        public int CurrentLevel { get; set; } = 1;

        public void Reset(int level = 1, int exp = 0)
        {
            CurrentLevel = level < 1 ? 1 : level;
            CurrentExp = exp < 0 ? 0 : exp;
        }

        public void Clear()
        {
            CurrentExp = 0;
            CurrentLevel = 1;
        }
    }

    public sealed class TreasureStorage
    {
        public int SchemaVersion { get; set; } = 1;
        public Dictionary<TREASURE_GRADE_TYPE, int> ChestCounts { get; } = new();
        public TreasureStorageProgress Progress { get; } = new();

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

        public void AddProgressExp(int amount)
        {
            if (amount <= 0)
                return;

            Progress.CurrentExp += amount;
        }

        public void ResetProgress(int level = 1, int exp = 0)
        {
            Progress.Reset(level, exp);
        }

        public void Clear()
        {
            SchemaVersion = 1;
            ChestCounts.Clear();
            Progress.Clear();
        }
    }
}
