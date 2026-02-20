using System.Collections.Generic;
using UnityEngine;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class RewardManager : CompoSingleton<RewardManager>
    {
        public void ApplyRewardGroupId(string rewardGroupId)
        {
            var deltas = ResolveRewardDeltas(rewardGroupId);
            ApplyRewardDatas(deltas);
        }

        public void ApplyRewardDatas(RewardData[] deltas)
        {
            Singleton.Get<InventoryManager>().AddRewards(deltas);
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
    }
}
