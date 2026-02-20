using System;
using UnityEngine;
using Devian.Domain.Common;
using Devian.Domain.Game;

namespace Devian
{
    public sealed class InventoryManager : CompoSingleton<InventoryManager>
    {
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

                if (r.Type != RewardType.Card && r.Type != RewardType.Currency && r.Type != RewardType.Equip && r.Type != RewardType.Hero)
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
                    _storage.AddCurrency(r.Id, r.Amount);
                }
                else if (r.Type == RewardType.Card)
                {
                    _applyCard(r.Id, r.Amount);
                }
                else if (r.Type == RewardType.Equip)
                {
                    _applyEquip(r.Id, r.Amount);
                }
                else // RewardType.Hero
                {
                    _applyHero(r.Id, r.Amount);
                }
            }

            return CommonResult.Ok();
        }

        public long GetAmount(string type, string id)
        {
            if (type == nameof(RewardType.Currency))
            {
                return _storage.GetCurrencyBalance(id);
            }

            if (type == nameof(RewardType.Card))
            {
                var card = _storage.GetCard(id);
                return card != null ? card.Amount : 0L;
            }

            if (type == nameof(RewardType.Equip))
            {
                return _storage.GetEquipsByEquipId(id).Count;
            }

            if (type == nameof(RewardType.Hero))
            {
                var hero = _storage.GetHero(id);
                return hero != null ? hero[StatType.UnitAmount] : 0L;
            }

            return 0L;
        }

        // ── Internal ──

        void _applyCard(string cardId, long amount)
        {
            var existing = _storage.GetCard(cardId);
            if (existing != null)
            {
                existing.AddAmount((int)amount);
            }
            else
            {
                var table = TB_CARD.Get(cardId);
                var ability = new AbilityCard();
                if (table != null)
                    ability.Init(table);

                _storage.AddCard(cardId, ability);
                ability.AddAmount((int)amount);
            }
        }

        void _applyEquip(string equipId, long amount)
        {
            var itemUid = Guid.NewGuid().ToString("N");
            var table = TB_EQUIP.Get(equipId);
            var ability = new AbilityEquip();
            if (table != null)
                ability.Init(table, itemUid);

            _storage.AddEquip(itemUid, ability);
        }

        void _applyHero(string heroId, long amount)
        {
            var existing = _storage.GetHero(heroId);
            if (existing != null)
            {
                existing.AddStat(StatType.UnitAmount, (int)amount);
            }
            else
            {
                var table = TB_UNIT_HERO.Get(heroId);
                var ability = new AbilityUnitHero();
                if (table != null)
                    ability.Init(table);

                _storage.AddHero(heroId, ability);
                ability.AddStat(StatType.UnitAmount, (int)amount);
            }
        }
    }
}
