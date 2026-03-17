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

        InventoryStorage storage => InventoryManager.Instance.Storage;

        // ──────────────────────────────────────────────
        //  OpenCollectedChests
        // ──────────────────────────────────────────────

        public CommonResult OpenCollectedChests(TREASURE_GRADE_TYPE gradeType)
        {
            if (gradeType == TREASURE_GRADE_TYPE.NONE)
                return CommonResult.Failure(COMMON_ERROR_TYPE.TREASURE_GRADE_INVALID, "gradeType NONE is not allowed.");

            var count = storage.GetTreasureCount(gradeType);
            if (count <= 0)
                return CommonResult.Ok();

            var rewardRows = TB_TREASURE_REWARD.GetByGroup(gradeType);
            if (rewardRows == null || rewardRows.Count == 0)
                return CommonResult.Failure(COMMON_ERROR_TYPE.TREASURE_REWARD_EMPTY, $"TREASURE_REWARD rows not found: {gradeType}");

            var bestRow = selectBestRewardRow(rewardRows);
            if (bestRow == null)
                return CommonResult.Failure(COMMON_ERROR_TYPE.TREASURE_REWARD_EMPTY, $"No TREASURE_REWARD row satisfied condition: {gradeType}");

            if (string.IsNullOrEmpty(bestRow.RewardGroupId))
                return CommonResult.Failure(COMMON_ERROR_TYPE.TREASURE_REWARD_GROUP_EMPTY, $"TREASURE_REWARD.RewardGroupId is empty: {gradeType}, level={bestRow.Level}");

            // apply rewards: count times × best row
            for (var c = 0; c < count; c++)
            {
                var applyResult = RewardManager.Instance.ApplyRewardGroup(bestRow.RewardGroupId);
                if (applyResult.IsFailure)
                {
                    Debug.LogError($"[{Tag}] OpenCollectedChests RewardManager.ApplyRewardGroup failed: {gradeType}, round={c}, rewardGroupId={bestRow.RewardGroupId}");
                    return CommonResult.Failure(COMMON_ERROR_TYPE.TREASURE_REWARD_APPLY_FAILED, $"RewardManager.ApplyRewardGroup failed: {bestRow.RewardGroupId}");
                }
            }

            // all-or-nothing: commit only after all rewards succeed
            storage.SetTreasureCount(gradeType, 0);
            return CommonResult.Ok();
        }

        // ──────────────────────────────────────────────
        //  OpenCurrentChest
        // ──────────────────────────────────────────────

        public CommonResult OpenCurrentChest()
        {
            var currentLevel = storage.TreasureCurrent.Level;

            var chestRow = TB_TREASURE_CHEST.Get(currentLevel);
            if (chestRow == null)
                return CommonResult.Failure(COMMON_ERROR_TYPE.TREASURE_CHEST_NOT_FOUND, $"TREASURE_CHEST row not found: level={currentLevel}");

            if (storage.TreasureCurrent.Exp < chestRow.MaxExp)
                return CommonResult.Ok();

            var gradeType = chestRow.TreasureGradeType;
            var rewardRows = TB_TREASURE_REWARD.GetByGroup(gradeType);
            if (rewardRows == null || rewardRows.Count == 0)
                return CommonResult.Failure(COMMON_ERROR_TYPE.TREASURE_REWARD_EMPTY, $"TREASURE_REWARD rows not found: {gradeType}");

            var bestRow = selectBestRewardRow(rewardRows);
            if (bestRow == null)
                return CommonResult.Failure(COMMON_ERROR_TYPE.TREASURE_REWARD_EMPTY, $"No TREASURE_REWARD row satisfied condition: {gradeType}");

            if (string.IsNullOrEmpty(bestRow.RewardGroupId))
                return CommonResult.Failure(COMMON_ERROR_TYPE.TREASURE_REWARD_GROUP_EMPTY, $"TREASURE_REWARD.RewardGroupId is empty: {gradeType}, level={bestRow.Level}");

            // apply reward: best row only
            var applyResult = RewardManager.Instance.ApplyRewardGroup(bestRow.RewardGroupId);
            if (applyResult.IsFailure)
            {
                Debug.LogError($"[{Tag}] OpenCurrentChest RewardManager.ApplyRewardGroup failed: level={currentLevel}, rewardGroupId={bestRow.RewardGroupId}");
                return CommonResult.Failure(COMMON_ERROR_TYPE.TREASURE_REWARD_APPLY_FAILED, $"RewardManager.ApplyRewardGroup failed: {bestRow.RewardGroupId}");
            }

            // all-or-nothing: commit only after reward succeeds
            storage.TreasureCurrent.Exp -= chestRow.MaxExp;
            storage.TreasureCurrent.Level++;

            var allChestRows = TB_TREASURE_CHEST.GetAll();
            var maxLevel = allChestRows.Count > 0 ? allChestRows.Max(r => r.Level) : 1;
            if (storage.TreasureCurrent.Level > maxLevel)
                storage.TreasureCurrent.Level = 1;

            return CommonResult.Ok();
        }

        // ──────────────────────────────────────────────
        //  Condition Selection
        // ──────────────────────────────────────────────

        /// <summary>
        /// 조건에 부합하는 row 중 가장 높은 Level을 선택한다.
        /// - conditionMsgId가 비어있으면 조건 통과 (조건 자체가 없음).
        /// - conditionMsgId가 있고 ConditionValue가 null이면 무조건 실패.
        /// - conditionMsgId가 있고 ConditionValue가 있으면 GameMessageManager.GetStat() 비교.
        /// </summary>
        static TREASURE_REWARD selectBestRewardRow(IReadOnlyList<TREASURE_REWARD> rows)
        {
            TREASURE_REWARD best = null;

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];

                if (!isConditionMet(row))
                    continue;

                if (best == null || row.Level > best.Level)
                    best = row;
            }

            return best;
        }

        static bool isConditionMet(TREASURE_REWARD row)
        {
            var msgId = row.ConditionMsgId;

            // conditionMsgId가 비어있으면 조건 자체가 없다 → 통과
            if (string.IsNullOrEmpty(msgId))
                return true;

            // conditionMsgId가 있는데 ConditionValue가 null이면 무조건 실패
            if (!row.ConditionValue.HasValue)
                return false;

            var stat = GameMessageManager.Instance.GetStat(msgId);
            return GameMessageRule.IsConditionSatisfied(stat, row.ConditionOp, row.ConditionValue.Value);
        }
    }
}
