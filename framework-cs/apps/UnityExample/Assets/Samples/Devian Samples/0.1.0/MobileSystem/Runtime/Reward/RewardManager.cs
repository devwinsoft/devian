using System;
using System.Collections.Generic;
using UnityEngine;
using Devian.Domain.Common;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class RewardManager : CompoSingleton<RewardManager>
    {
        public CommonResult<RewardApplyResult> ApplyRewardGroup(string rewardGroupId, int rewardAmountMultiplier = 1)
        {
            var normalizedRewardGroupId = rewardGroupId != null ? rewardGroupId.Trim() : string.Empty;
            if (string.IsNullOrEmpty(normalizedRewardGroupId))
            {
                return CommonResult<RewardApplyResult>.Success(
                    new RewardApplyResult(string.Empty, Array.Empty<RewardData>()));
            }

            var multiplier = rewardAmountMultiplier < 1 ? 1 : rewardAmountMultiplier;
            var deltas = ResolveRewardDatas(normalizedRewardGroupId);
            if (multiplier > 1 && deltas.Length > 0)
                deltas = scaleRewardAmounts(deltas, multiplier);

            var apply = ApplyRewardDatas(deltas);
            if (apply.IsFailure)
                return CommonResult<RewardApplyResult>.Failure(apply.Error!);

            return CommonResult<RewardApplyResult>.Success(
                new RewardApplyResult(normalizedRewardGroupId, deltas ?? Array.Empty<RewardData>()));
        }

        public CommonResult ApplyRewardDatas(RewardData[] deltas)
        {
            return Singleton.Get<InventoryManager>().AddRewards(deltas);
        }

        public RewardData[] ResolveRewardDatas(string rewardGroupId)
        {
            var normalizedRewardGroupId = rewardGroupId != null ? rewardGroupId.Trim() : string.Empty;
            if (string.IsNullOrEmpty(normalizedRewardGroupId))
                return Array.Empty<RewardData>();

            var resolveGuard = new HashSet<string>(StringComparer.Ordinal);
            return ResolveRewardDeltasInternal(normalizedRewardGroupId, resolveGuard);
        }

        RewardData[] ResolveRewardDeltasInternal(string rewardGroupId, HashSet<string> resolveGuard)
        {
            if (!resolveGuard.Add(rewardGroupId))
            {
                Debug.LogWarning($"[RewardManager] Circular reward group reference detected: {rewardGroupId}");
                return Array.Empty<RewardData>();
            }

            try
            {
                var rows = TB_REWARD.GetByGroup(rewardGroupId);
                if (rows == null || rows.Count == 0)
                    return Array.Empty<RewardData>();

                if (!TrySelectRewardRow(rows, out var selectedReward))
                    return Array.Empty<RewardData>();

                if (selectedReward.Type == REWARD_TYPE.TREASURE)
                    return ResolveTreasureRewardDeltas(selectedReward, resolveGuard);

                return new[] { new RewardData(selectedReward.Type, selectedReward.Id, selectedReward.Amount) };
            }
            finally
            {
                resolveGuard.Remove(rewardGroupId);
            }
        }

        RewardData[] ResolveTreasureRewardDeltas(REWARD chestReward, HashSet<string> resolveGuard)
        {
            var chestId = chestReward.Id != null ? chestReward.Id.Trim() : string.Empty;
            if (string.IsNullOrEmpty(chestId) || chestReward.Amount <= 0)
                return Array.Empty<RewardData>();

            var chestRows = TB_ITEM_TREASURE.GetByGroup(chestId);
            if (chestRows == null || chestRows.Count == 0)
            {
                Debug.LogWarning($"[RewardManager] ITEM_TREASURE rows not found: chestId={chestId}");
                return Array.Empty<RewardData>();
            }

            var list = new List<RewardData>();
            for (var openCount = 0; openCount < chestReward.Amount; openCount++)
            {
                for (var i = 0; i < chestRows.Count; i++)
                {
                    var row = chestRows[i];
                    if (row == null)
                        continue;

                    var nestedRewardGroupId = row.RewardGroupId != null ? row.RewardGroupId.Trim() : string.Empty;
                    if (string.IsNullOrEmpty(nestedRewardGroupId))
                        continue;

                    var nestedRewards = ResolveRewardDeltasInternal(nestedRewardGroupId, resolveGuard);
                    if (nestedRewards.Length == 0)
                        continue;

                    list.AddRange(nestedRewards);
                }
            }

            return list.Count == 0 ? Array.Empty<RewardData>() : list.ToArray();
        }

        static bool TrySelectRewardRow(IReadOnlyList<REWARD> rows, out REWARD selectedReward)
        {
            selectedReward = null;

            var totalRate = 0f;
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (!IsSelectableRewardRow(row))
                    continue;

                totalRate += row.Rate;
            }

            if (!(totalRate > 0f))
                return false;

            var roll = UnityEngine.Random.value * totalRate;
            var cumulative = 0f;
            REWARD lastReward = null;

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (!IsSelectableRewardRow(row))
                    continue;

                cumulative += row.Rate;
                lastReward = row;
                if (roll < cumulative)
                {
                    selectedReward = row;
                    return true;
                }
            }

            if (lastReward == null)
                return false;

            selectedReward = lastReward;
            return true;
        }

        static bool IsSelectableRewardRow(REWARD row)
        {
            if (row == null)
                return false;

            if (string.IsNullOrWhiteSpace(row.Id) || row.Amount <= 0)
                return false;

            var rate = row.Rate;
            if (float.IsNaN(rate) || float.IsInfinity(rate))
                return false;

            return rate > 0f;
        }

        static RewardData[] scaleRewardAmounts(RewardData[] source, int multiplier)
        {
            var list = new RewardData[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                var reward = source[i];
                var scaledAmountLong = (long)reward.Amount * multiplier;
                var scaledAmount = scaledAmountLong > int.MaxValue
                    ? int.MaxValue
                    : (int)scaledAmountLong;
                list[i] = new RewardData(reward.Type, reward.Id, scaledAmount);
            }

            return list;
        }

        public readonly struct RewardApplyResult
        {
            public RewardApplyResult(string rewardGroupId, RewardData[] appliedRewards)
            {
                RewardGroupId = rewardGroupId ?? string.Empty;
                AppliedRewards = appliedRewards ?? Array.Empty<RewardData>();
            }

            public string RewardGroupId { get; }
            public RewardData[] AppliedRewards { get; }
        }
    }
}
