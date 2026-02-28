using System;
using System.Collections.Generic;
using UnityEngine;
using Devian.Domain.Common;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class RewardManager : CompoSingleton<RewardManager>
    {
        public CommonResult<RewardApplyResult> ApplyRewardGroup(string rewardGroupId)
        {
            if (string.IsNullOrEmpty(rewardGroupId))
            {
                return CommonResult<RewardApplyResult>.Success(
                    new RewardApplyResult(string.Empty, Array.Empty<RewardData>()));
            }

            var deltas = ResolveRewardDeltas(rewardGroupId);
            var apply = ApplyRewardDatas(deltas);
            if (apply.IsFailure)
                return CommonResult<RewardApplyResult>.Failure(apply.Error!);

            return CommonResult<RewardApplyResult>.Success(
                new RewardApplyResult(rewardGroupId, deltas ?? Array.Empty<RewardData>()));
        }

        public CommonResult ApplyRewardDatas(RewardData[] deltas)
        {
            return Singleton.Get<InventoryManager>().AddRewards(deltas);
        }

        RewardData[] ResolveRewardDeltas(string rewardGroupId)
        {
            var rows = TB_REWARD.GetByGroup(rewardGroupId);
            var list = new List<RewardData>(rows.Count);

            foreach (var row in rows)
            {
                if (string.IsNullOrEmpty(row.Id) || row.Amount <= 0)
                    continue;

                list.Add(new RewardData(row.Type, row.Id, row.Amount));
            }

            return list.ToArray();
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
