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

        // ──────────────────────────────────────────────
        //  OpenCollectedChests
        // ──────────────────────────────────────────────

        public GameResult OpenCollectedChests(TREASURE_GRADE_TYPE gradeType)
        {
            if (gradeType == TREASURE_GRADE_TYPE.NONE)
                return GameResult.Failure(GAME_ERROR_TYPE.TREASURE_GRADE_INVALID, "gradeType NONE is not allowed.");

            var inventoryManager = InventoryManager.Instance;
            var count = inventoryManager.GetTreasureCount(gradeType);
            if (count <= 0)
                return GameResult.Ok();

            var rewardRows = TB_TREASURE_REWARD.GetByGroup(gradeType);
            if (rewardRows == null || rewardRows.Count == 0)
                return GameResult.Failure(GAME_ERROR_TYPE.TREASURE_REWARD_EMPTY, $"TREASURE_REWARD rows not found: {gradeType}");

            var bestRow = selectBestRewardRow(rewardRows);
            if (bestRow == null)
                return GameResult.Failure(GAME_ERROR_TYPE.TREASURE_REWARD_EMPTY, $"No TREASURE_REWARD row satisfied condition: {gradeType}");

            if (string.IsNullOrEmpty(bestRow.reward_group_id))
                return GameResult.Failure(GAME_ERROR_TYPE.TREASURE_REWARD_GROUP_EMPTY, $"TREASURE_REWARD.reward_group_id is empty: {gradeType}, level={bestRow.level}");

            // apply rewards: count times × best row
            for (var c = 0; c < count; c++)
            {
                var applyResult = RewardManager.Instance.ApplyRewardGroup(bestRow.reward_group_id);
                if (applyResult.IsFailure)
                {
                    Debug.LogError($"[{Tag}] OpenCollectedChests RewardManager.ApplyRewardGroup failed: {gradeType}, round={c}, reward_group_id={bestRow.reward_group_id}");
                    return GameResult.Failure(applyResult.Error!);
                }
            }

            // all-or-nothing: commit only after all rewards succeed
            var revoke = InventoryManager.Instance.RevokeTreasure(gradeType, count);
            if (revoke.IsFailure)
                return revoke;

            return GameResult.Ok();
        }

        // ──────────────────────────────────────────────
        //  OpenCurrentChest
        // ──────────────────────────────────────────────

        public GameResult OpenCurrentChest()
        {
            var inventoryManager = InventoryManager.Instance;
            var currentLevel = inventoryManager.GetTreasureCurrentLevel();

            var chestRow = TB_TREASURE_CHEST.Get(currentLevel);
            if (chestRow == null)
                return GameResult.Failure(GAME_ERROR_TYPE.TREASURE_CHEST_NOT_FOUND, $"TREASURE_CHEST row not found: level={currentLevel}");

            var currentExp = inventoryManager.GetTreasureCurrentExp();
            if (currentExp < chestRow.max_exp)
                return GameResult.Ok();

            var gradeType = chestRow.treasure_grade_type;
            var rewardRows = TB_TREASURE_REWARD.GetByGroup(gradeType);
            if (rewardRows == null || rewardRows.Count == 0)
                return GameResult.Failure(GAME_ERROR_TYPE.TREASURE_REWARD_EMPTY, $"TREASURE_REWARD rows not found: {gradeType}");

            var bestRow = selectBestRewardRow(rewardRows);
            if (bestRow == null)
                return GameResult.Failure(GAME_ERROR_TYPE.TREASURE_REWARD_EMPTY, $"No TREASURE_REWARD row satisfied condition: {gradeType}");

            if (string.IsNullOrEmpty(bestRow.reward_group_id))
                return GameResult.Failure(GAME_ERROR_TYPE.TREASURE_REWARD_GROUP_EMPTY, $"TREASURE_REWARD.reward_group_id is empty: {gradeType}, level={bestRow.level}");

            // apply reward: best row only
            var applyResult = RewardManager.Instance.ApplyRewardGroup(bestRow.reward_group_id);
            if (applyResult.IsFailure)
            {
                Debug.LogError($"[{Tag}] OpenCurrentChest RewardManager.ApplyRewardGroup failed: level={currentLevel}, reward_group_id={bestRow.reward_group_id}");
                return GameResult.Failure(applyResult.Error!);
            }

            // all-or-nothing: commit only after reward succeeds
            var nextExp = currentExp - chestRow.max_exp;
            var nextLevel = currentLevel + 1;

            var allChestRows = TB_TREASURE_CHEST.GetAll();
            var maxLevel = allChestRows.Count > 0 ? allChestRows.Max(r => r.level) : 1;
            if (nextLevel > maxLevel)
                nextLevel = 1;

            inventoryManager.SetTreasureCurrentState(nextLevel, nextExp);

            return GameResult.Ok();
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

                if (best == null || row.level > best.level)
                    best = row;
            }

            return best;
        }

        static bool isConditionMet(TREASURE_REWARD row)
        {
            var msgId = row.condition_msg_id;

            // conditionMsgId가 비어있으면 조건 자체가 없다 → 통과
            if (string.IsNullOrEmpty(msgId))
                return true;

            // conditionMsgId가 있는데 ConditionValue가 null이면 무조건 실패
            if (!row.condition_value.HasValue)
                return false;

            var stat = GameMessageManager.Instance.GetStat(msgId);
            return GameMessageRule.IsConditionSatisfied(stat, row.condition_op, row.condition_value.Value);
        }
    }
}
