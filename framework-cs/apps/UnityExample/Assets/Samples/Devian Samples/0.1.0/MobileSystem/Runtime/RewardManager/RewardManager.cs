using System.Collections.Generic;
using UnityEngine;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class RewardManager : CompoSingleton<RewardManager>
    {
        public void ApplyRewardId(string rewardId)
        {
            var deltas = ResolveRewardDeltas(rewardId);
            ApplyRewardDatas(deltas);
        }

        public void ApplyRewardDatas(RewardData[] deltas)
        {
            Singleton.Get<InventoryManager>().AddRewards(deltas);
        }

        // (REQ-2) 원격 호출 없음 → 동기 변환 (TB_REWARD 직접 참조)
        RewardData[] ResolveRewardDeltas(string rewardId)
        {
            var row = TB_REWARD.Get(rewardId);

            var list = new List<RewardData>(8);

            addItem(list, row.ItemId_00, row.ItemCount_00);
            addItem(list, row.ItemId_01, row.ItemCount_01);
            addItem(list, row.ItemId_02, row.ItemCount_02);
            addItem(list, row.ItemId_03, row.ItemCount_03);

            if (row.CurrencyAmount > 0)
            {
                // (REQ-7) amount는 long 유지
                list.Add(new RewardData(RewardType.Currency, row.CurrencyType.ToString(), row.CurrencyAmount));
            }

            return list.ToArray();
        }

        static void addItem(List<RewardData> list, string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0)
                return;

            list.Add(new RewardData(RewardType.Item, itemId, (long)count));
        }
    }
}
