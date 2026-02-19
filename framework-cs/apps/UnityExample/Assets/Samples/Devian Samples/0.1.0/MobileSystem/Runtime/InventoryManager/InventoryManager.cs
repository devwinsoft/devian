using System.Collections.Generic;
using UnityEngine;
using Devian.Domain.Common;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class InventoryManager : CompoSingleton<InventoryManager>
    {
        readonly Dictionary<string, long> _currencyBalances = new();
        readonly InventoryStorage _storage = new();

        public InventoryStorage Storage => _storage;

        // ── Public API ──

        public CommonResult AddRewards(RewardData[] rewards)
        {
            if (rewards == null)
                return CommonResult.Failure(CommonErrorType.INVENTORY_DELTAS_NULL, "rewards is null");

            if (rewards.Length == 0)
                return CommonResult.Ok();

            // ── 선검증 (all-or-nothing) ──
            for (int i = 0; i < rewards.Length; i++)
            {
                var r = rewards[i];

                if (r.Type != RewardType.Item && r.Type != RewardType.Currency)
                    return CommonResult.Failure(CommonErrorType.INVENTORY_DELTA_TYPE_INVALID,
                        $"rewards[{i}] invalid type: {r.Type}");

                if (string.IsNullOrWhiteSpace(r.Id))
                    return CommonResult.Failure(CommonErrorType.INVENTORY_DELTA_ID_EMPTY,
                        $"rewards[{i}] id is empty");

                if (r.Amount < 0)
                    return CommonResult.Failure(CommonErrorType.INVENTORY_DELTA_AMOUNT_NEGATIVE,
                        $"rewards[{i}] amount is negative: {r.Amount}");
            }

            // ── Apply ──
            for (int i = 0; i < rewards.Length; i++)
            {
                var r = rewards[i];

                if (r.Amount == 0)
                    continue;

                if (r.Type == RewardType.Currency)
                {
                    _applyCurrency(r.Id, r.Amount);
                }
                else // RewardType.Item
                {
                    _applyItem(r.Id, r.Amount);
                }
            }

            return CommonResult.Ok();
        }

        public long GetAmount(string type, string id)
        {
            if (type == nameof(RewardType.Currency))
            {
                return _currencyBalances.TryGetValue(id, out var balance) ? balance : 0L;
            }

            if (type == nameof(RewardType.Item))
            {
                var item = _storage.GetItem(id);
                return item != null ? item.Count : 0L;
            }

            return 0L;
        }

        // ── Internal ──

        void _applyCurrency(string currencyId, long amount)
        {
            if (_currencyBalances.TryGetValue(currencyId, out var current))
                _currencyBalances[currencyId] = current + amount;
            else
                _currencyBalances[currencyId] = amount;
        }

        void _applyItem(string itemId, long amount)
        {
            var existing = _storage.GetItem(itemId);
            if (existing != null)
            {
                existing.AddCount(amount);
            }
            else
            {
                var table = TB_ITEM.Get(itemId);
                var ability = new ItemAbility();
                if (table != null)
                    ability.Init(table);

                var data = _storage.AddItem(itemId, ability);
                data.AddCount(amount);
            }
        }
    }
}
