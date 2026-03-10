using System;
using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Common;
using Devian.Domain.Game;
using UnityEngine;

namespace Devian
{
    public sealed class InventoryManager : CompoSingleton<InventoryManager>
    {
        readonly InventoryStorage _storage = new();
        readonly InventoryMessageTrigger _messageTrigger = new();

        public InventoryStorage Storage => _storage;

        // ── Public API ──

        public void Subcribe(EntityId ownerKey, MESSAGE_INVENTORY_TYPE msgType, BaseTrigger<EntityId, MESSAGE_INVENTORY_TYPE>.Handler handler)
        {
            _messageTrigger.Subcribe(ownerKey, msgType, handler);
        }

        public void SubcribeOnce(EntityId ownerKey, MESSAGE_INVENTORY_TYPE msgType, Action<object[]> handler)
        {
            _messageTrigger.SubcribeOnce(ownerKey, msgType, handler);
        }

        public void UnSubcribe(EntityId ownerKey)
        {
            _messageTrigger.UnSubcribe(ownerKey);
        }

        public void SetPassOwnership(string passId, bool owned)
        {
            setPassOwnership(passId, owned);
        }

        public void RemovePassOwnership(string passId)
        {
            removePassOwnership(passId);
        }

        public async Task<CommonResult> FirstInitAsync(CancellationToken ct = default)
        {
#if UNITY_EDITOR
            return CommonResult.Ok();
#else
            if (!AccountManager.TryGet(out var accountManager))
            {
                return CommonResult.Failure(
                    CommonErrorType.COMMON_SERVER,
                    "AccountManager is not available.");
            }

            if (accountManager.IsLocalOnlySaveMode || !accountManager.HasAuthenticatedSession)
                return CommonResult.Ok();

            var fetch = await FirebaseCallableManager.Instance.GetInitialInventoryAsync(ct);
            if (fetch.IsFailure)
                return CommonResult.Failure(fetch.Error!);

            var rewards = fetch.Value ?? Array.Empty<RewardData>();
            if (rewards.Length == 0)
                return CommonResult.Ok();

            try
            {
                return AddRewards(rewards);
            }
            catch (Exception ex)
            {
                return CommonResult.Failure(
                    CommonErrorType.COMMON_SERVER,
                    $"FirstInit apply failed: {ex.Message}");
            }
#endif
        }

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

                if (r.Type != REWARD_TYPE.CARD && r.Type != REWARD_TYPE.CURRENCY && r.Type != REWARD_TYPE.EQUIP && r.Type != REWARD_TYPE.HERO && r.Type != REWARD_TYPE.RENTAL && r.Type != REWARD_TYPE.PASS)
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

                if (r.Type == REWARD_TYPE.CURRENCY)
                {
                    var currencyType = (CURRENCY_TYPE)Enum.Parse(typeof(CURRENCY_TYPE), r.Id);
                    _storage.AddCurrency(currencyType, r.Amount);
                }
                else if (r.Type == REWARD_TYPE.CARD)
                {
                    _applyCard(r.Id, r.Amount);
                }
                else if (r.Type == REWARD_TYPE.EQUIP)
                {
                    _applyEquip(r.Id, r.Amount);
                }
                else if (r.Type == REWARD_TYPE.HERO)
                {
                    _applyHero(r.Id, r.Amount);
                }
                else if (r.Type == REWARD_TYPE.RENTAL)
                {
                    // Amount=1은 활성화 flag. 만료 시각은 데이터 Sync에서 서버로부터 갱신.
                    _storage.SetRental(r.Id, long.MaxValue);
                }
                else if (r.Type == REWARD_TYPE.PASS)
                {
                    setPassOwnership(r.Id, true);
                }
            }

            return CommonResult.Ok();
        }

        public CommonResult RevokeRewards(RewardData[] rewards)
        {
            if (rewards == null)
                return CommonResult.Failure(CommonErrorType.INVENTORY_DELTAS_NULL, "rewards is null");

            if (rewards.Length == 0)
                return CommonResult.Ok();

            // Validate first (all-or-nothing).
            for (int i = 0; i < rewards.Length; i++)
            {
                var r = rewards[i];

                if (r.Type != REWARD_TYPE.CARD && r.Type != REWARD_TYPE.CURRENCY && r.Type != REWARD_TYPE.EQUIP && r.Type != REWARD_TYPE.HERO && r.Type != REWARD_TYPE.RENTAL && r.Type != REWARD_TYPE.PASS)
                    return CommonResult.Failure(CommonErrorType.INVENTORY_DELTA_TYPE_INVALID,
                        $"rewards[{i}] invalid type: {r.Type}");

                if (string.IsNullOrWhiteSpace(r.Id))
                    return CommonResult.Failure(CommonErrorType.INVENTORY_DELTA_ID_EMPTY,
                        $"rewards[{i}] id is empty");

                if (r.Amount < 0)
                    return CommonResult.Failure(CommonErrorType.INVENTORY_DELTA_AMOUNT_NEGATIVE,
                        $"rewards[{i}] amount is negative: {r.Amount}");

                if (r.Amount == 0)
                    continue;

                if (r.Type == REWARD_TYPE.CURRENCY)
                {
                    if (!Enum.TryParse<CURRENCY_TYPE>(r.Id, out var currencyType))
                    {
                        return CommonResult.Failure(
                            CommonErrorType.INVENTORY_DELTA_ID_EMPTY,
                            $"rewards[{i}] invalid currency id: {r.Id}");
                    }

                    var balance = _storage.GetCurrencyBalance(currencyType);
                    if (balance < r.Amount)
                    {
                        return CommonResult.Failure(
                            CommonErrorType.INVENTORY_REFUND_INSUFFICIENT,
                            $"rewards[{i}] insufficient currency. id={r.Id} need={r.Amount} have={balance}");
                    }
                }
                else if (r.Type == REWARD_TYPE.CARD)
                {
                    var card = _storage.GetCard(r.Id);
                    var amount = card != null ? card.Amount : 0L;
                    if (amount < r.Amount)
                    {
                        return CommonResult.Failure(
                            CommonErrorType.INVENTORY_REFUND_INSUFFICIENT,
                            $"rewards[{i}] insufficient card amount. id={r.Id} need={r.Amount} have={amount}");
                    }
                }
                else if (r.Type == REWARD_TYPE.EQUIP)
                {
                    var count = _storage.GetEquipsByEquipId(r.Id).Count;
                    if (count < r.Amount)
                    {
                        return CommonResult.Failure(
                            CommonErrorType.INVENTORY_REFUND_INSUFFICIENT,
                            $"rewards[{i}] insufficient equip count. id={r.Id} need={r.Amount} have={count}");
                    }
                }
                else if (r.Type == REWARD_TYPE.HERO)
                {
                    var hero = _storage.GetHero(r.Id);
                    var amount = hero != null ? hero[STAT_TYPE.UNIT_AMOUNT] : 0L;
                    if (amount < r.Amount)
                    {
                        return CommonResult.Failure(
                            CommonErrorType.INVENTORY_REFUND_INSUFFICIENT,
                            $"rewards[{i}] insufficient hero amount. id={r.Id} need={r.Amount} have={amount}");
                    }
                }
                else if (r.Type == REWARD_TYPE.RENTAL)
                {
                    if (!_storage.HasActiveRental(r.Id))
                    {
                        return CommonResult.Failure(
                            CommonErrorType.INVENTORY_REFUND_INSUFFICIENT,
                            $"rewards[{i}] rental not active. id={r.Id}");
                    }
                }
                else if (r.Type == REWARD_TYPE.PASS)
                {
                    if (!_storage.HasPass(r.Id))
                    {
                        return CommonResult.Failure(
                            CommonErrorType.INVENTORY_REFUND_INSUFFICIENT,
                            $"rewards[{i}] pass not owned. id={r.Id}");
                    }
                }
            }

            // Apply revoke.
            for (int i = 0; i < rewards.Length; i++)
            {
                var r = rewards[i];
                if (r.Amount == 0)
                    continue;

                if (r.Type == REWARD_TYPE.CURRENCY)
                {
                    var currencyType = (CURRENCY_TYPE)Enum.Parse(typeof(CURRENCY_TYPE), r.Id);
                    _storage.AddCurrency(currencyType, -r.Amount);
                }
                else if (r.Type == REWARD_TYPE.CARD)
                {
                    var card = _storage.GetCard(r.Id);
                    if (card == null)
                    {
                        return CommonResult.Failure(
                            CommonErrorType.PURCHASE_REFUND_APPLY_FAILED,
                            $"Card disappeared while revoking reward. id={r.Id}");
                    }
                    card.AddAmount(-(int)r.Amount);
                }
                else if (r.Type == REWARD_TYPE.EQUIP)
                {
                    var equips = _storage.GetEquipsByEquipId(r.Id);
                    for (var j = 0; j < r.Amount; j++)
                    {
                        if (j >= equips.Count || equips[j] == null || string.IsNullOrEmpty(equips[j].ItemUid) ||
                            !_storage.RemoveEquip(equips[j].ItemUid))
                        {
                            return CommonResult.Failure(
                                CommonErrorType.PURCHASE_REFUND_APPLY_FAILED,
                                $"Equip revoke failed while removing item. equipId={r.Id}");
                        }
                    }
                }
                else if (r.Type == REWARD_TYPE.HERO)
                {
                    var hero = _storage.GetHero(r.Id);
                    if (hero == null)
                    {
                        return CommonResult.Failure(
                            CommonErrorType.PURCHASE_REFUND_APPLY_FAILED,
                            $"Hero disappeared while revoking reward. id={r.Id}");
                    }
                    hero.AddStat(STAT_TYPE.UNIT_AMOUNT, -(int)r.Amount);
                }
                else if (r.Type == REWARD_TYPE.RENTAL)
                {
                    _storage.RemoveRental(r.Id);
                }
                else if (r.Type == REWARD_TYPE.PASS)
                {
                    removePassOwnership(r.Id);
                }
            }

            return CommonResult.Ok();
        }

        /// <summary>
        /// 보상을 가능한 만큼 회수한다 (partial revoke).
        /// 잔액이 부족하면 보유량까지만 차감하고 성공으로 처리한다.
        /// type/id/amount 데이터 오류는 여전히 Failure를 반환한다.
        /// </summary>
        public CommonResult RevokeRewardsPartial(RewardData[] rewards)
        {
            if (rewards == null)
                return CommonResult.Failure(CommonErrorType.INVENTORY_DELTAS_NULL, "rewards is null");

            if (rewards.Length == 0)
                return CommonResult.Ok();

            // 기본 유효성 검증 (type, id, amount 부호 — 데이터 오류이므로 Failure)
            for (int i = 0; i < rewards.Length; i++)
            {
                var r = rewards[i];

                if (r.Type != REWARD_TYPE.CARD && r.Type != REWARD_TYPE.CURRENCY && r.Type != REWARD_TYPE.EQUIP && r.Type != REWARD_TYPE.HERO && r.Type != REWARD_TYPE.RENTAL && r.Type != REWARD_TYPE.PASS)
                    return CommonResult.Failure(CommonErrorType.INVENTORY_DELTA_TYPE_INVALID,
                        $"rewards[{i}] invalid type: {r.Type}");

                if (string.IsNullOrWhiteSpace(r.Id))
                    return CommonResult.Failure(CommonErrorType.INVENTORY_DELTA_ID_EMPTY,
                        $"rewards[{i}] id is empty");

                if (r.Amount < 0)
                    return CommonResult.Failure(CommonErrorType.INVENTORY_DELTA_AMOUNT_NEGATIVE,
                        $"rewards[{i}] amount is negative: {r.Amount}");

                if (r.Amount == 0)
                    continue;

                if (r.Type == REWARD_TYPE.CURRENCY)
                {
                    if (!Enum.TryParse<CURRENCY_TYPE>(r.Id, out _))
                    {
                        return CommonResult.Failure(
                            CommonErrorType.INVENTORY_DELTA_ID_EMPTY,
                            $"rewards[{i}] invalid currency id: {r.Id}");
                    }
                }
            }

            // 클램프 + 적용 (보유량 이하로 차감)
            for (int i = 0; i < rewards.Length; i++)
            {
                var r = rewards[i];
                if (r.Amount == 0)
                    continue;

                if (r.Type == REWARD_TYPE.CURRENCY)
                {
                    var currencyType = (CURRENCY_TYPE)Enum.Parse(typeof(CURRENCY_TYPE), r.Id);
                    var balance = _storage.GetCurrencyBalance(currencyType);
                    var clampedAmount = Math.Min(r.Amount, balance);
                    if (clampedAmount > 0)
                        _storage.AddCurrency(currencyType, -clampedAmount);
                }
                else if (r.Type == REWARD_TYPE.CARD)
                {
                    var card = _storage.GetCard(r.Id);
                    var have = card != null ? card.Amount : 0L;
                    var clampedAmount = (int)Math.Min(r.Amount, have);
                    if (clampedAmount > 0 && card != null)
                        card.AddAmount(-clampedAmount);
                }
                else if (r.Type == REWARD_TYPE.EQUIP)
                {
                    var equips = _storage.GetEquipsByEquipId(r.Id);
                    var clampedAmount = (int)Math.Min(r.Amount, equips.Count);
                    for (var j = 0; j < clampedAmount; j++)
                    {
                        if (j < equips.Count && equips[j] != null && !string.IsNullOrEmpty(equips[j].ItemUid))
                            _storage.RemoveEquip(equips[j].ItemUid);
                    }
                }
                else if (r.Type == REWARD_TYPE.HERO)
                {
                    var hero = _storage.GetHero(r.Id);
                    if (hero != null)
                    {
                        var have = hero[STAT_TYPE.UNIT_AMOUNT];
                        var clampedAmount = (int)Math.Min(r.Amount, have);
                        if (clampedAmount > 0)
                            hero.AddStat(STAT_TYPE.UNIT_AMOUNT, -clampedAmount);
                    }
                }
                else if (r.Type == REWARD_TYPE.RENTAL)
                {
                    if (_storage.HasActiveRental(r.Id))
                        _storage.RemoveRental(r.Id);
                }
                else if (r.Type == REWARD_TYPE.PASS)
                {
                    if (_storage.HasPass(r.Id))
                        removePassOwnership(r.Id);
                }
            }

            return CommonResult.Ok();
        }

        public long GetAmount(string type, string id)
        {
            if (type == nameof(REWARD_TYPE.CURRENCY))
            {
                var currencyType = (CURRENCY_TYPE)Enum.Parse(typeof(CURRENCY_TYPE), id);
                return _storage.GetCurrencyBalance(currencyType);
            }

            if (type == nameof(REWARD_TYPE.CARD))
            {
                var card = _storage.GetCard(id);
                return card != null ? card.Amount : 0L;
            }

            if (type == nameof(REWARD_TYPE.EQUIP))
            {
                return _storage.GetEquipsByEquipId(id).Count;
            }

            if (type == nameof(REWARD_TYPE.HERO))
            {
                var hero = _storage.GetHero(id);
                return hero != null ? hero[STAT_TYPE.UNIT_AMOUNT] : 0L;
            }

            if (type == nameof(REWARD_TYPE.RENTAL))
            {
                return _storage.HasActiveRental(id) ? 1L : 0L;
            }

            if (type == nameof(REWARD_TYPE.PASS))
            {
                return _storage.HasPass(id) ? 1L : 0L;
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
                var table = TB_ITEM_CARD.Get(cardId);
                var ability = new AbilityCard();
                if (table != null)
                    ability.Init(table);

                _storage.AddCard(cardId, ability);
                ability.AddAmount((int)amount);
            }
        }

        void _applyEquip(string equipId, long amount)
        {
            if (amount <= 0)
                return;

            var table = TB_ITEM_EQUIP.Get(equipId);
            for (var i = 0; i < amount; i++)
            {
                var itemUid = Guid.NewGuid().ToString("N");
                var ability = new AbilityEquip();
                if (table != null)
                    ability.Init(table, itemUid);

                _storage.AddEquip(itemUid, ability);
            }
        }

        void _applyHero(string heroId, long amount)
        {
            var existing = _storage.GetHero(heroId);
            if (existing != null)
            {
                existing.AddStat(STAT_TYPE.UNIT_AMOUNT, (int)amount);
            }
            else
            {
                var table = TB_UNIT_HERO.Get(heroId);
                var ability = new AbilityUnitHero();
                if (table != null)
                    ability.Init(table);

                _storage.AddHero(heroId, ability);
                ability.AddStat(STAT_TYPE.UNIT_AMOUNT, (int)amount);
            }
        }

        void setPassOwnership(string passId, bool owned)
        {
            if (_storage.SetPass(passId, owned))
                _messageTrigger.Notify(MESSAGE_INVENTORY_TYPE.PASS_CHANGED, passId, owned);
        }

        void removePassOwnership(string passId)
        {
            if (_storage.RemovePass(passId))
                _messageTrigger.Notify(MESSAGE_INVENTORY_TYPE.PASS_CHANGED, passId, false);
        }
    }
}
