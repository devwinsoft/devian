using System.Collections.Generic;
using System.Linq;
using Devian.Domain.Common;
using Devian.Domain.Game;
using UnityEngine;

namespace Devian
{
    public sealed class TreasureManager : CompoSingleton<TreasureManager>
    {
        const string Tag = "TreasureManager";

        readonly TreasureStorage _storage = new();
        public TreasureStorage Storage => _storage;

        // ──────────────────────────────────────────────
        //  CollectChest
        // ──────────────────────────────────────────────

        public CommonResult CollectChest(TREASURE_GRADE_TYPE gradeType)
        {
            if (gradeType == TREASURE_GRADE_TYPE.NONE)
                return CommonResult.Failure(COMMON_ERROR_TYPE.TREASURE_GRADE_INVALID, "gradeType NONE is not allowed.");

            var count = _storage.GetChestCount(gradeType);
            if (count <= 0)
                return CommonResult.Ok();

            var chestRow = TB_TREASURE_CHEST.Get(gradeType);
            if (chestRow == null)
                return CommonResult.Failure(COMMON_ERROR_TYPE.TREASURE_CHEST_NOT_FOUND, $"TREASURE_CHEST row not found: {gradeType}");

            var treasureGroupId = chestRow.TreasureGroupId;
            if (string.IsNullOrEmpty(treasureGroupId))
                return CommonResult.Failure(COMMON_ERROR_TYPE.TREASURE_GROUP_EMPTY, $"TREASURE_CHEST.TreasureGroupId is empty: {gradeType}");

            var groupRows = TB_TREASURE_GROUP.GetByGroup(treasureGroupId);
            if (groupRows == null || groupRows.Count == 0)
                return CommonResult.Failure(COMMON_ERROR_TYPE.TREASURE_GROUP_EMPTY, $"TREASURE_GROUP rows not found: {treasureGroupId}");

            // validate all rewardGroupIds before applying
            for (var i = 0; i < groupRows.Count; i++)
            {
                if (string.IsNullOrEmpty(groupRows[i].RewardGroupId))
                    return CommonResult.Failure(COMMON_ERROR_TYPE.TREASURE_REWARD_GROUP_EMPTY, $"TREASURE_GROUP[{i}].RewardGroupId is empty: {treasureGroupId}");
            }

            // apply rewards: count times × group rows
            for (var c = 0; c < count; c++)
            {
                for (var g = 0; g < groupRows.Count; g++)
                {
                    var applyResult = RewardManager.Instance.ApplyRewardGroup(groupRows[g].RewardGroupId);
                    if (applyResult.IsFailure)
                    {
                        Debug.LogError($"[{Tag}] CollectChest RewardManager.ApplyRewardGroup failed: {gradeType}, round={c}, group={g}, rewardGroupId={groupRows[g].RewardGroupId}");
                        return CommonResult.Failure(COMMON_ERROR_TYPE.TREASURE_REWARD_APPLY_FAILED, $"RewardManager.ApplyRewardGroup failed: {groupRows[g].RewardGroupId}");
                    }
                }
            }

            // all-or-nothing: commit only after all rewards succeed
            _storage.SetChestCount(gradeType, 0);
            return CommonResult.Ok();
        }

        // ──────────────────────────────────────────────
        //  CollectProgress
        // ──────────────────────────────────────────────

        public CommonResult CollectProgress()
        {
            var currentLevel = _storage.Progress.CurrentLevel;

            var progressRow = TB_TREASURE_PROGRESS.Get(currentLevel);
            if (progressRow == null)
                return CommonResult.Failure(COMMON_ERROR_TYPE.TREASURE_PROGRESS_NOT_FOUND, $"TREASURE_PROGRESS row not found: level={currentLevel}");

            if (_storage.Progress.CurrentExp < progressRow.MaxExp)
                return CommonResult.Ok();

            var treasureGroupId = progressRow.TreasureGroupId;
            if (string.IsNullOrEmpty(treasureGroupId))
                return CommonResult.Failure(COMMON_ERROR_TYPE.TREASURE_GROUP_EMPTY, $"TREASURE_PROGRESS.TreasureGroupId is empty: level={currentLevel}");

            var groupRows = TB_TREASURE_GROUP.GetByGroup(treasureGroupId);
            if (groupRows == null || groupRows.Count == 0)
                return CommonResult.Failure(COMMON_ERROR_TYPE.TREASURE_GROUP_EMPTY, $"TREASURE_GROUP rows not found: {treasureGroupId}");

            // validate all rewardGroupIds before applying
            for (var i = 0; i < groupRows.Count; i++)
            {
                if (string.IsNullOrEmpty(groupRows[i].RewardGroupId))
                    return CommonResult.Failure(COMMON_ERROR_TYPE.TREASURE_REWARD_GROUP_EMPTY, $"TREASURE_GROUP[{i}].RewardGroupId is empty: {treasureGroupId}");
            }

            // apply rewards
            for (var g = 0; g < groupRows.Count; g++)
            {
                var applyResult = RewardManager.Instance.ApplyRewardGroup(groupRows[g].RewardGroupId);
                if (applyResult.IsFailure)
                {
                    Debug.LogError($"[{Tag}] CollectProgress RewardManager.ApplyRewardGroup failed: level={currentLevel}, group={g}, rewardGroupId={groupRows[g].RewardGroupId}");
                    return CommonResult.Failure(COMMON_ERROR_TYPE.TREASURE_REWARD_APPLY_FAILED, $"RewardManager.ApplyRewardGroup failed: {groupRows[g].RewardGroupId}");
                }
            }

            // all-or-nothing: commit only after all rewards succeed
            _storage.Progress.CurrentExp -= progressRow.MaxExp;
            _storage.Progress.CurrentLevel++;

            var allProgressRows = TB_TREASURE_PROGRESS.GetAll();
            var maxLevel = allProgressRows.Count > 0 ? allProgressRows.Max(r => r.Level) : 1;
            if (_storage.Progress.CurrentLevel > maxLevel)
                _storage.Progress.CurrentLevel = 1;

            return CommonResult.Ok();
        }
    }
}
