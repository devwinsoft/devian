using System;
using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Common;
using Devian.Domain.Game;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Devian
{
    public sealed class InventoryManager : CompoSingleton<InventoryManager>
    {
        const long DefaultRentalDurationMs = 30L * 24L * 60L * 60L * 1000L;

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
            ct.ThrowIfCancellationRequested();

            var parse = parseInitialInventoryRewards();
            if (parse.IsFailure)
                return CommonResult.Failure(parse.Error!);

            var rewards = parse.Value ?? Array.Empty<RewardData>();
            if (rewards.Length == 0)
                return CommonResult.Ok();

            var apply = AddRewards(rewards);
            if (apply.IsFailure)
                return CommonResult.Failure(apply.Error!);

            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            return CommonResult.Ok();
        }

        static CommonResult<RewardData[]> parseInitialInventoryRewards()
        {
            var setting = Resources.Load<InventorySetting>(InventorySetting.ResourcesPath);
            if (setting == null)
            {
                return CommonResult<RewardData[]>.Failure(
                    COMMON_ERROR_TYPE.COMMON_SERVER,
                    $"InventorySetting is not available. expected={InventorySetting.DefaultResourcesAssetPath}");
            }

            var json = ((string)setting.InitialInventory)?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(json))
                return CommonResult<RewardData[]>.Success(Array.Empty<RewardData>());

            JToken root;
            try
            {
                root = JToken.Parse(json);
            }
            catch (Exception ex)
            {
                return CommonResult<RewardData[]>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    $"InitialInventory JSON parse failed: {ex.Message}");
            }

            JArray rewardsArray = null;
            if (root is JArray rootArray)
            {
                rewardsArray = rootArray;
            }
            else if (root is JObject rootObj && rootObj["rewards"] is JArray nestedArray)
            {
                rewardsArray = nestedArray;
            }

            if (rewardsArray == null)
            {
                return CommonResult<RewardData[]>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "InitialInventory must be RewardData[] JSON or {\"rewards\": RewardData[]}.");
            }

            if (rewardsArray.Count == 0)
                return CommonResult<RewardData[]>.Success(Array.Empty<RewardData>());

            var rewards = new RewardData[rewardsArray.Count];
            for (var i = 0; i < rewardsArray.Count; i++)
            {
                if (rewardsArray[i] is not JObject rewardObj)
                {
                    return CommonResult<RewardData[]>.Failure(
                        COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                        $"InitialInventory[{i}] must be an object.");
                }

                var typeText = (rewardObj.Value<string>("type") ?? string.Empty).Trim();
                if (string.Equals(typeText, "SEASON_PASS", StringComparison.OrdinalIgnoreCase))
                    typeText = "PASS";

                if (!Enum.TryParse(typeText, true, out REWARD_TYPE rewardType))
                {
                    return CommonResult<RewardData[]>.Failure(
                        COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                        $"InitialInventory[{i}].type is invalid: {typeText}");
                }

                var id = (rewardObj.Value<string>("id") ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(id))
                {
                    return CommonResult<RewardData[]>.Failure(
                        COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                        $"InitialInventory[{i}].id is empty.");
                }

                var amountToken = rewardObj["amount"];
                if (amountToken == null || amountToken.Type != JTokenType.Integer)
                {
                    return CommonResult<RewardData[]>.Failure(
                        COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                        $"InitialInventory[{i}].amount must be an integer.");
                }

                var amountLong = amountToken.Value<long>();
                if (amountLong <= 0 || amountLong > int.MaxValue)
                {
                    return CommonResult<RewardData[]>.Failure(
                        COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                        $"InitialInventory[{i}].amount must be within 1..{int.MaxValue}.");
                }

                rewards[i] = new RewardData(rewardType, id, (int)amountLong);
            }

            return CommonResult<RewardData[]>.Success(rewards);
        }

        public CommonResult AddRewards(RewardData[] rewards)
        {
            if (rewards == null)
                return CommonResult.Failure(COMMON_ERROR_TYPE.INVENTORY_DELTAS_NULL, "rewards is null");

            if (rewards.Length == 0)
                return CommonResult.Ok();

            // ── 선검증 (all-or-nothing) ──
            for (int i = 0; i < rewards.Length; i++)
            {
                var r = rewards[i];

                if (r.Type != REWARD_TYPE.CARD && r.Type != REWARD_TYPE.CURRENCY && r.Type != REWARD_TYPE.EQUIP && r.Type != REWARD_TYPE.HERO && r.Type != REWARD_TYPE.RENTAL && r.Type != REWARD_TYPE.PASS)
                    return CommonResult.Failure(COMMON_ERROR_TYPE.INVENTORY_DELTA_TYPE_INVALID,
                        $"rewards[{i}] invalid type: {r.Type}");

                if (string.IsNullOrWhiteSpace(r.Id))
                    return CommonResult.Failure(COMMON_ERROR_TYPE.INVENTORY_DELTA_ID_EMPTY,
                        $"rewards[{i}] id is empty");

                if (r.Amount < 0)
                    return CommonResult.Failure(COMMON_ERROR_TYPE.INVENTORY_DELTA_AMOUNT_NEGATIVE,
                        $"rewards[{i}] amount is negative: {r.Amount}");

                if (r.Type == REWARD_TYPE.CURRENCY)
                {
                    if (!Enum.TryParse<CURRENCY_TYPE>(r.Id, out var currencyType) ||
                        currencyType == CURRENCY_TYPE.ADS ||
                        currencyType == CURRENCY_TYPE.FREE ||
                        currencyType == CURRENCY_TYPE.JEWEL)
                    {
                        return CommonResult.Failure(
                            COMMON_ERROR_TYPE.INVENTORY_DELTA_ID_EMPTY,
                            $"rewards[{i}] invalid currency id: {r.Id}");
                    }
                }
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
                    if (!_storage.Wallet.TryAdd(currencyType, r.Amount))
                    {
                        return CommonResult.Failure(
                            COMMON_ERROR_TYPE.INVENTORY_DELTA_ID_EMPTY,
                            $"rewards[{i}] invalid currency id: {r.Id}");
                    }
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
                    var nowUtcMs = RemoteDataManager.ServerNowUtcMs;
                    var currentExpiryUtcMs = _storage.GetRentalExpiry(r.Id);
                    var baseUtcMs = currentExpiryUtcMs > nowUtcMs ? currentExpiryUtcMs : nowUtcMs;
                    _storage.SetRental(r.Id, baseUtcMs + DefaultRentalDurationMs);
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
                return CommonResult.Failure(COMMON_ERROR_TYPE.INVENTORY_DELTAS_NULL, "rewards is null");

            if (rewards.Length == 0)
                return CommonResult.Ok();

            // Validate first (all-or-nothing).
            for (int i = 0; i < rewards.Length; i++)
            {
                var r = rewards[i];

                if (r.Type != REWARD_TYPE.CARD && r.Type != REWARD_TYPE.CURRENCY && r.Type != REWARD_TYPE.EQUIP && r.Type != REWARD_TYPE.HERO && r.Type != REWARD_TYPE.RENTAL && r.Type != REWARD_TYPE.PASS)
                    return CommonResult.Failure(COMMON_ERROR_TYPE.INVENTORY_DELTA_TYPE_INVALID,
                        $"rewards[{i}] invalid type: {r.Type}");

                if (string.IsNullOrWhiteSpace(r.Id))
                    return CommonResult.Failure(COMMON_ERROR_TYPE.INVENTORY_DELTA_ID_EMPTY,
                        $"rewards[{i}] id is empty");

                if (r.Amount < 0)
                    return CommonResult.Failure(COMMON_ERROR_TYPE.INVENTORY_DELTA_AMOUNT_NEGATIVE,
                        $"rewards[{i}] amount is negative: {r.Amount}");

                if (r.Amount == 0)
                    continue;

                if (r.Type == REWARD_TYPE.CURRENCY)
                {
                    if (!Enum.TryParse<CURRENCY_TYPE>(r.Id, out var currencyType))
                    {
                        return CommonResult.Failure(
                            COMMON_ERROR_TYPE.INVENTORY_DELTA_ID_EMPTY,
                            $"rewards[{i}] invalid currency id: {r.Id}");
                    }

                    if (currencyType == CURRENCY_TYPE.ADS ||
                        currencyType == CURRENCY_TYPE.FREE ||
                        currencyType == CURRENCY_TYPE.JEWEL)
                    {
                        return CommonResult.Failure(
                            COMMON_ERROR_TYPE.INVENTORY_DELTA_ID_EMPTY,
                            $"rewards[{i}] invalid currency id: {r.Id}");
                    }

                    var balance = _storage.Wallet.Get(currencyType);
                    if (balance < r.Amount)
                    {
                        return CommonResult.Failure(
                            COMMON_ERROR_TYPE.INVENTORY_REFUND_INSUFFICIENT,
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
                            COMMON_ERROR_TYPE.INVENTORY_REFUND_INSUFFICIENT,
                            $"rewards[{i}] insufficient card amount. id={r.Id} need={r.Amount} have={amount}");
                    }
                }
                else if (r.Type == REWARD_TYPE.EQUIP)
                {
                    var count = _storage.GetEquipsByEquipId(r.Id).Count;
                    if (count < r.Amount)
                    {
                        return CommonResult.Failure(
                            COMMON_ERROR_TYPE.INVENTORY_REFUND_INSUFFICIENT,
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
                            COMMON_ERROR_TYPE.INVENTORY_REFUND_INSUFFICIENT,
                            $"rewards[{i}] insufficient hero amount. id={r.Id} need={r.Amount} have={amount}");
                    }
                }
                else if (r.Type == REWARD_TYPE.RENTAL)
                {
                    if (!_storage.HasActiveRental(r.Id))
                    {
                        return CommonResult.Failure(
                            COMMON_ERROR_TYPE.INVENTORY_REFUND_INSUFFICIENT,
                            $"rewards[{i}] rental not active. id={r.Id}");
                    }
                }
                else if (r.Type == REWARD_TYPE.PASS)
                {
                    if (!_storage.HasPass(r.Id))
                    {
                        return CommonResult.Failure(
                            COMMON_ERROR_TYPE.INVENTORY_REFUND_INSUFFICIENT,
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
                    if (!_storage.Wallet.TryAdd(currencyType, -r.Amount))
                    {
                        return CommonResult.Failure(
                            COMMON_ERROR_TYPE.PURCHASE_REFUND_APPLY_FAILED,
                            $"Currency revoke failed while applying reward. id={r.Id}");
                    }
                }
                else if (r.Type == REWARD_TYPE.CARD)
                {
                    var card = _storage.GetCard(r.Id);
                    if (card == null)
                    {
                        return CommonResult.Failure(
                            COMMON_ERROR_TYPE.PURCHASE_REFUND_APPLY_FAILED,
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
                                COMMON_ERROR_TYPE.PURCHASE_REFUND_APPLY_FAILED,
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
                            COMMON_ERROR_TYPE.PURCHASE_REFUND_APPLY_FAILED,
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
                return CommonResult.Failure(COMMON_ERROR_TYPE.INVENTORY_DELTAS_NULL, "rewards is null");

            if (rewards.Length == 0)
                return CommonResult.Ok();

            // 기본 유효성 검증 (type, id, amount 부호 — 데이터 오류이므로 Failure)
            for (int i = 0; i < rewards.Length; i++)
            {
                var r = rewards[i];

                if (r.Type != REWARD_TYPE.CARD && r.Type != REWARD_TYPE.CURRENCY && r.Type != REWARD_TYPE.EQUIP && r.Type != REWARD_TYPE.HERO && r.Type != REWARD_TYPE.RENTAL && r.Type != REWARD_TYPE.PASS)
                    return CommonResult.Failure(COMMON_ERROR_TYPE.INVENTORY_DELTA_TYPE_INVALID,
                        $"rewards[{i}] invalid type: {r.Type}");

                if (string.IsNullOrWhiteSpace(r.Id))
                    return CommonResult.Failure(COMMON_ERROR_TYPE.INVENTORY_DELTA_ID_EMPTY,
                        $"rewards[{i}] id is empty");

                if (r.Amount < 0)
                    return CommonResult.Failure(COMMON_ERROR_TYPE.INVENTORY_DELTA_AMOUNT_NEGATIVE,
                        $"rewards[{i}] amount is negative: {r.Amount}");

                if (r.Amount == 0)
                    continue;

                if (r.Type == REWARD_TYPE.CURRENCY)
                {
                    if (!Enum.TryParse<CURRENCY_TYPE>(r.Id, out var currencyType) ||
                        currencyType == CURRENCY_TYPE.ADS ||
                        currencyType == CURRENCY_TYPE.FREE ||
                        currencyType == CURRENCY_TYPE.JEWEL)
                    {
                        return CommonResult.Failure(
                            COMMON_ERROR_TYPE.INVENTORY_DELTA_ID_EMPTY,
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
                    var balance = _storage.Wallet.Get(currencyType);
                    var clampedAmount = Math.Min(r.Amount, balance);
                    if (clampedAmount > 0)
                        _storage.Wallet.TryAdd(currencyType, -clampedAmount);
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
                return _storage.Wallet.Get(currencyType);
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
