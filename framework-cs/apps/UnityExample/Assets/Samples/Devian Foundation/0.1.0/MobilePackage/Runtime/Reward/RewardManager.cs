using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Devian.Domain.Common;
using Devian.Domain.Game;
using Newtonsoft.Json.Linq;

namespace Devian
{
    public sealed class RewardManager : CompoSingleton<RewardManager>
    {
        readonly struct FirstSelectedHeroEquipGrant
        {
            public FirstSelectedHeroEquipGrant(EQUIP_SLOT_TYPE slotType, string itemId)
            {
                SlotType = slotType;
                ItemId = itemId ?? string.Empty;
            }

            public EQUIP_SLOT_TYPE SlotType { get; }
            public string ItemId { get; }
        }

        // ── Apply RewardGroup ──

        public GameResult<RewardApplyResult> ApplyRewardGroup(string rewardGroupId, int rewardAmountMultiplier = 1)
        {
            var normalizedRewardGroupId = rewardGroupId != null ? rewardGroupId.Trim() : string.Empty;
            if (string.IsNullOrEmpty(normalizedRewardGroupId))
            {
                return GameResult<RewardApplyResult>.Success(
                    new RewardApplyResult(string.Empty, Array.Empty<RewardData>()));
            }

            var multiplier = rewardAmountMultiplier < 1 ? 1 : rewardAmountMultiplier;
            var deltas = ResolveRewardDatas(normalizedRewardGroupId);
            if (multiplier > 1 && deltas.Length > 0)
                deltas = scaleRewardAmounts(deltas, multiplier);

            var apply = ApplyRewardDatas(deltas);
            if (apply.IsFailure)
                return GameResult<RewardApplyResult>.Failure(apply.Error!);

            return GameResult<RewardApplyResult>.Success(
                new RewardApplyResult(normalizedRewardGroupId, deltas ?? Array.Empty<RewardData>()));
        }

        // ── Apply RewardDatas (type switch + validation) ──

        public GameResult ApplyRewardDatas(RewardData[] rewards)
        {
            if (rewards == null)
                return GameResult.Failure(GAME_ERROR_TYPE.INVENTORY_DELTAS_NULL, "rewards is null");

            if (rewards.Length == 0)
                return GameResult.Ok();

            // ── 선검증 (all-or-nothing) ──
            for (int i = 0; i < rewards.Length; i++)
            {
                var v = _validateRewardData(rewards[i], i);
                if (v.IsFailure) return v;
            }

            var inv = InventoryManager.Instance;

            // ── Apply ──
            for (int i = 0; i < rewards.Length; i++)
            {
                var r = rewards[i];

                if (r.Amount == 0)
                    continue;

                if (r.Type == REWARD_TYPE.CURRENCY)
                {
                    var currencyType = (CURRENCY_TYPE)Enum.Parse(typeof(CURRENCY_TYPE), r.Id);
                    var apply = inv.ApplyCurrency(currencyType, r.Amount);
                    if (apply.IsFailure)
                        return apply;
                }
                else if (r.Type == REWARD_TYPE.CARD)
                {
                    var apply = inv.ApplyCard(r.Id, (int)r.Amount);
                    if (apply.IsFailure)
                        return apply;
                }
                else if (r.Type == REWARD_TYPE.MATERIAL)
                {
                    var apply = inv.ApplyMaterial(r.Id, (int)r.Amount);
                    if (apply.IsFailure)
                        return apply;
                }
                else if (r.Type == REWARD_TYPE.EQUIP)
                {
                    var apply = inv.ApplyEquip(r.Id, (int)r.Amount);
                    if (apply.IsFailure)
                        return apply;
                }
                else if (r.Type == REWARD_TYPE.HERO)
                {
                    var apply = inv.ApplyHero(r.Id, (int)r.Amount);
                    if (apply.IsFailure)
                        return apply;
                }
                else if (r.Type == REWARD_TYPE.RENTAL)
                {
                    var apply = inv.ApplyRental(r.Id);
                    if (apply.IsFailure)
                        return apply;
                }
                else if (r.Type == REWARD_TYPE.PASS)
                {
                    var apply = inv.SetPassOwnership(r.Id, true);
                    if (apply.IsFailure)
                        return apply;
                }
                else if (r.Type == REWARD_TYPE.TREASURE)
                {
                    var gradeType = (ITEM_GRADE_TYPE)Enum.Parse(typeof(ITEM_GRADE_TYPE), r.Id);
                    var apply = inv.ApplyTreasure(gradeType, (int)r.Amount);
                    if (apply.IsFailure)
                        return apply;
                }
            }

            return GameResult.Ok();
        }

        // ── Revoke RewardDatas ──

        public GameResult RevokeRewardDatas(RewardData[] rewards)
        {
            if (rewards == null)
                return GameResult.Failure(GAME_ERROR_TYPE.INVENTORY_DELTAS_NULL, "rewards is null");

            if (rewards.Length == 0)
                return GameResult.Ok();

            var inv = InventoryManager.Instance;

            // Validate first (all-or-nothing).
            for (int i = 0; i < rewards.Length; i++)
            {
                var r = rewards[i];

                var v = _validateRewardData(r, i);
                if (v.IsFailure) return v;

                if (r.Amount == 0)
                    continue;

                if (r.Type == REWARD_TYPE.CURRENCY)
                {
                    var currencyType = (CURRENCY_TYPE)Enum.Parse(typeof(CURRENCY_TYPE), r.Id);
                    var balance = inv.GetCurrencyAmount(currencyType);
                    if (balance < r.Amount)
                    {
                        return GameResult.Failure(
                            GAME_ERROR_TYPE.INVENTORY_REFUND_INSUFFICIENT,
                            $"rewards[{i}] insufficient currency. id={r.Id} need={r.Amount} have={balance}");
                    }
                }
                else if (r.Type == REWARD_TYPE.CARD)
                {
                    var amount = inv.GetCardAmount(r.Id);
                    if (amount < r.Amount)
                    {
                        return GameResult.Failure(
                            GAME_ERROR_TYPE.INVENTORY_REFUND_INSUFFICIENT,
                            $"rewards[{i}] insufficient card amount. id={r.Id} need={r.Amount} have={amount}");
                    }
                }
                else if (r.Type == REWARD_TYPE.MATERIAL)
                {
                    var amount = inv.GetMaterialAmount(r.Id);
                    if (amount < r.Amount)
                    {
                        return GameResult.Failure(
                            GAME_ERROR_TYPE.INVENTORY_REFUND_INSUFFICIENT,
                            $"rewards[{i}] insufficient material amount. id={r.Id} need={r.Amount} have={amount}");
                    }
                }
                else if (r.Type == REWARD_TYPE.EQUIP)
                {
                    var count = inv.GetEquipCount(r.Id);
                    if (count < r.Amount)
                    {
                        return GameResult.Failure(
                            GAME_ERROR_TYPE.INVENTORY_REFUND_INSUFFICIENT,
                            $"rewards[{i}] insufficient equip count. id={r.Id} need={r.Amount} have={count}");
                    }
                }
                else if (r.Type == REWARD_TYPE.HERO)
                {
                    var amount = inv.GetHeroAmount(r.Id);
                    if (amount < r.Amount)
                    {
                        return GameResult.Failure(
                            GAME_ERROR_TYPE.INVENTORY_REFUND_INSUFFICIENT,
                            $"rewards[{i}] insufficient hero amount. id={r.Id} need={r.Amount} have={amount}");
                    }
                }
                else if (r.Type == REWARD_TYPE.RENTAL)
                {
                    if (!inv.HasActiveRental(r.Id))
                    {
                        return GameResult.Failure(
                            GAME_ERROR_TYPE.INVENTORY_REFUND_INSUFFICIENT,
                            $"rewards[{i}] rental not active. id={r.Id}");
                    }
                }
                else if (r.Type == REWARD_TYPE.PASS)
                {
                    if (!inv.HasPass(r.Id))
                    {
                        return GameResult.Failure(
                            GAME_ERROR_TYPE.INVENTORY_REFUND_INSUFFICIENT,
                            $"rewards[{i}] pass not owned. id={r.Id}");
                    }
                }
                else if (r.Type == REWARD_TYPE.TREASURE)
                {
                    var gradeType = (ITEM_GRADE_TYPE)Enum.Parse(typeof(ITEM_GRADE_TYPE), r.Id);
                    var chestCount = inv.GetTreasureCount(gradeType);
                    if (chestCount < r.Amount)
                    {
                        return GameResult.Failure(
                            GAME_ERROR_TYPE.INVENTORY_REFUND_INSUFFICIENT,
                            $"rewards[{i}] insufficient treasure chest count. id={r.Id} need={r.Amount} have={chestCount}");
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
                    var revoke = inv.RevokeCurrency(currencyType, r.Amount);
                    if (revoke.IsFailure)
                        return revoke;
                }
                else if (r.Type == REWARD_TYPE.CARD)
                {
                    var revoke = inv.RevokeCard(r.Id, (int)r.Amount);
                    if (revoke.IsFailure)
                        return revoke;
                }
                else if (r.Type == REWARD_TYPE.MATERIAL)
                {
                    var revoke = inv.RevokeMaterial(r.Id, (int)r.Amount);
                    if (revoke.IsFailure)
                        return revoke;
                }
                else if (r.Type == REWARD_TYPE.EQUIP)
                {
                    var revoke = inv.RevokeEquip(r.Id, (int)r.Amount);
                    if (revoke.IsFailure)
                        return revoke;
                }
                else if (r.Type == REWARD_TYPE.HERO)
                {
                    var revoke = inv.RevokeHero(r.Id, (int)r.Amount);
                    if (revoke.IsFailure)
                        return revoke;
                }
                else if (r.Type == REWARD_TYPE.RENTAL)
                {
                    var revoke = inv.RevokeRental(r.Id);
                    if (revoke.IsFailure)
                        return revoke;
                }
                else if (r.Type == REWARD_TYPE.PASS)
                {
                    inv.RemovePassOwnership(r.Id);
                }
                else if (r.Type == REWARD_TYPE.TREASURE)
                {
                    var gradeType = (ITEM_GRADE_TYPE)Enum.Parse(typeof(ITEM_GRADE_TYPE), r.Id);
                    var revoke = inv.RevokeTreasure(gradeType, (int)r.Amount);
                    if (revoke.IsFailure)
                        return revoke;
                }
            }

            return GameResult.Ok();
        }

        // ── Revoke Partial ──

        public GameResult RevokeRewardDatasPartial(RewardData[] rewards)
        {
            if (rewards == null)
                return GameResult.Failure(GAME_ERROR_TYPE.INVENTORY_DELTAS_NULL, "rewards is null");

            if (rewards.Length == 0)
                return GameResult.Ok();

            // 기본 유효성 검증 (type, id, amount 부호 — 데이터 오류이므로 Failure)
            for (int i = 0; i < rewards.Length; i++)
            {
                var v = _validateRewardData(rewards[i], i);
                if (v.IsFailure) return v;
            }

            var inv = InventoryManager.Instance;

            // 클램프 + 적용 (보유량 이하로 차감)
            for (int i = 0; i < rewards.Length; i++)
            {
                var r = rewards[i];
                if (r.Amount == 0)
                    continue;

                if (r.Type == REWARD_TYPE.CURRENCY)
                {
                    var currencyType = (CURRENCY_TYPE)Enum.Parse(typeof(CURRENCY_TYPE), r.Id);
                    var balance = inv.GetCurrencyAmount(currencyType);
                    var clampedAmount = Math.Min(r.Amount, balance);
                    if (clampedAmount > 0)
                    {
                        var revoke = inv.RevokeCurrency(currencyType, clampedAmount);
                        if (revoke.IsFailure)
                            return revoke;
                    }
                }
                else if (r.Type == REWARD_TYPE.CARD)
                {
                    var have = inv.GetCardAmount(r.Id);
                    var clampedAmount = (int)Math.Min(r.Amount, have);
                    if (clampedAmount > 0)
                    {
                        var revoke = inv.RevokeCard(r.Id, clampedAmount);
                        if (revoke.IsFailure)
                            return revoke;
                    }
                }
                else if (r.Type == REWARD_TYPE.MATERIAL)
                {
                    var have = inv.GetMaterialAmount(r.Id);
                    var clampedAmount = (int)Math.Min(r.Amount, have);
                    if (clampedAmount > 0)
                    {
                        var revoke = inv.RevokeMaterial(r.Id, clampedAmount);
                        if (revoke.IsFailure)
                            return revoke;
                    }
                }
                else if (r.Type == REWARD_TYPE.EQUIP)
                {
                    var count = inv.GetEquipCount(r.Id);
                    var clampedAmount = (int)Math.Min(r.Amount, count);
                    if (clampedAmount > 0)
                    {
                        var revoke = inv.RevokeEquip(r.Id, clampedAmount);
                        if (revoke.IsFailure)
                            return revoke;
                    }
                }
                else if (r.Type == REWARD_TYPE.HERO)
                {
                    var have = inv.GetHeroAmount(r.Id);
                    var clampedAmount = (int)Math.Min(r.Amount, have);
                    if (clampedAmount > 0)
                    {
                        var revoke = inv.RevokeHero(r.Id, clampedAmount);
                        if (revoke.IsFailure)
                            return revoke;
                    }
                }
                else if (r.Type == REWARD_TYPE.RENTAL)
                {
                    if (inv.HasActiveRental(r.Id))
                    {
                        var revoke = inv.RevokeRental(r.Id);
                        if (revoke.IsFailure)
                            return revoke;
                    }
                }
                else if (r.Type == REWARD_TYPE.PASS)
                {
                    if (inv.HasPass(r.Id))
                        inv.RemovePassOwnership(r.Id);
                }
                else if (r.Type == REWARD_TYPE.TREASURE)
                {
                    var gradeType = (ITEM_GRADE_TYPE)Enum.Parse(typeof(ITEM_GRADE_TYPE), r.Id);
                    var have = inv.GetTreasureCount(gradeType);
                    var clampedAmount = (int)Math.Min(r.Amount, have);
                    if (clampedAmount > 0)
                    {
                        var revoke = inv.RevokeTreasure(gradeType, clampedAmount);
                        if (revoke.IsFailure)
                            return revoke;
                    }
                }
            }

            return GameResult.Ok();
        }

        // ── GetAmount ──

        public long GetAmount(string type, string id)
        {
            var inv = InventoryManager.Instance;

            if (type == nameof(REWARD_TYPE.CURRENCY))
            {
                var currencyType = (CURRENCY_TYPE)Enum.Parse(typeof(CURRENCY_TYPE), id);
                return inv.GetCurrencyAmount(currencyType);
            }

            if (type == nameof(REWARD_TYPE.CARD))
                return inv.GetCardAmount(id);

            if (type == nameof(REWARD_TYPE.MATERIAL))
                return inv.GetMaterialAmount(id);

            if (type == nameof(REWARD_TYPE.EQUIP))
                return inv.GetEquipCount(id);

            if (type == nameof(REWARD_TYPE.HERO))
                return inv.GetHeroAmount(id);

            if (type == nameof(REWARD_TYPE.RENTAL))
                return inv.HasActiveRental(id) ? 1L : 0L;

            if (type == nameof(REWARD_TYPE.PASS))
                return inv.HasPass(id) ? 1L : 0L;

            if (type == nameof(REWARD_TYPE.TREASURE))
            {
                var gradeType = (ITEM_GRADE_TYPE)Enum.Parse(typeof(ITEM_GRADE_TYPE), id);
                return inv.GetTreasureCount(gradeType);
            }

            return 0L;
        }

        // ── FirstInitAsync ──

        public async Task<GameResult> FirstInitAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var loadSetting = _loadFirstRewardSettings();
            if (loadSetting.IsFailure)
                return GameResult.Failure(loadSetting.Error!);

            var setting = loadSetting.Value;

            var parse = _parseFirstRewardSettings(setting);
            if (parse.IsFailure)
                return GameResult.Failure(parse.Error!);

            var inv = InventoryManager.Instance;
            var selectedHeroItem = resolveFirstSelectedHeroItem(setting);
            var selectedHeroItemId = selectedHeroItem?.item_id?.Trim() ?? string.Empty;
            var selectedHeroEquipGrants = collectFirstSelectedHeroEquipGrants(selectedHeroItem);
            var previousEquipUidsByItemId = captureOwnedEquipUids(inv, selectedHeroEquipGrants);

            var rewards = appendFirstSelectedHeroReward(parse.Value ?? Array.Empty<RewardData>(), selectedHeroItemId);
            rewards = appendFirstSelectedHeroEquipRewards(rewards, selectedHeroEquipGrants);
            if (rewards.Length > 0)
            {
                var apply = ApplyRewardDatas(rewards);
                if (apply.IsFailure)
                    return GameResult.Failure(apply.Error!);
            }

            // Stamina: 설정 로드 → MaxStamina 지급
            inv.LoadSettings();

            int maxStamina = inv.MaxStamina;
            if (maxStamina > 0)
            {
                var apply = inv.ApplyCurrency(CURRENCY_TYPE.STAMINA, maxStamina);
                if (apply.IsFailure)
                    return GameResult.Failure(apply.Error!);
            }

            var applyInitialEquips = applyFirstSelectedHeroInitialEquips(
                inv,
                selectedHeroItemId,
                selectedHeroEquipGrants,
                previousEquipUidsByItemId);
            if (applyInitialEquips.IsFailure)
                return GameResult.Failure(applyInitialEquips.Error!);

            inv.SelectedHeroId = selectedHeroItemId;
            if (!string.IsNullOrEmpty(selectedHeroItemId)
                && string.IsNullOrEmpty(inv.SelectedHeroId))
            {
                var selectedHeroUnitId = setting.SelectedHeroUnitId?.Value ?? string.Empty;
                Debug.LogWarning(
                    $"[RewardManager] FirstRewardSettings selected hero is not owned after first init. unitId={selectedHeroUnitId}, itemId={selectedHeroItemId}");
            }

            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            return GameResult.Ok();
        }

        // ── Resolve ──

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

                if (selectedReward.type == REWARD_TYPE.TREASURE)
                    return ResolveTreasureRewardDeltas(selectedReward, resolveGuard);

                return new[] { new RewardData(selectedReward.type, selectedReward.id, selectedReward.amount) };
            }
            finally
            {
                resolveGuard.Remove(rewardGroupId);
            }
        }

        RewardData[] ResolveTreasureRewardDeltas(REWARD chestReward, HashSet<string> resolveGuard)
        {
            var chestId = chestReward.id != null ? chestReward.id.Trim() : string.Empty;
            if (string.IsNullOrEmpty(chestId) || chestReward.amount <= 0)
                return Array.Empty<RewardData>();

            var chestRows = TB_ITEM_TREASURE.GetByGroup(chestId);
            if (chestRows == null || chestRows.Count == 0)
            {
                Debug.LogWarning($"[RewardManager] ITEM_TREASURE rows not found: chestId={chestId}");
                return Array.Empty<RewardData>();
            }

            var list = new List<RewardData>();
            for (var openCount = 0; openCount < chestReward.amount; openCount++)
            {
                for (var i = 0; i < chestRows.Count; i++)
                {
                    var row = chestRows[i];
                    if (row == null)
                        continue;

                    var nestedRewardGroupId = row.reward_group_id != null ? row.reward_group_id.Trim() : string.Empty;
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

                totalRate += row.rate;
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

                cumulative += row.rate;
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

            if (string.IsNullOrWhiteSpace(row.id) || row.amount <= 0)
                return false;

            var rate = row.rate;
            if (float.IsNaN(rate) || float.IsInfinity(rate))
                return false;

            return rate > 0f;
        }

        // ── Private ──

        static GameResult _validateRewardData(RewardData r, int index)
        {
            if (r.Type != REWARD_TYPE.CARD && r.Type != REWARD_TYPE.CURRENCY &&
                r.Type != REWARD_TYPE.MATERIAL &&
                r.Type != REWARD_TYPE.EQUIP && r.Type != REWARD_TYPE.HERO &&
                r.Type != REWARD_TYPE.RENTAL && r.Type != REWARD_TYPE.PASS &&
                r.Type != REWARD_TYPE.TREASURE)
                return GameResult.Failure(GAME_ERROR_TYPE.INVENTORY_DELTA_TYPE_INVALID,
                    $"rewards[{index}] invalid type: {r.Type}");

            if (string.IsNullOrWhiteSpace(r.Id))
                return GameResult.Failure(GAME_ERROR_TYPE.INVENTORY_DELTA_ID_EMPTY,
                    $"rewards[{index}] id is empty");

            if (r.Amount < 0)
                return GameResult.Failure(GAME_ERROR_TYPE.INVENTORY_DELTA_AMOUNT_NEGATIVE,
                    $"rewards[{index}] amount is negative: {r.Amount}");

            if (r.Type == REWARD_TYPE.CURRENCY)
            {
                if (!Enum.TryParse<CURRENCY_TYPE>(r.Id, out var currencyType) ||
                    currencyType == CURRENCY_TYPE.ADS ||
                    currencyType == CURRENCY_TYPE.FREE ||
                    currencyType == CURRENCY_TYPE.JEWEL)
                {
                    return GameResult.Failure(
                        GAME_ERROR_TYPE.INVENTORY_DELTA_ID_EMPTY,
                        $"rewards[{index}] invalid currency id: {r.Id}");
                }
            }

            if (r.Type == REWARD_TYPE.TREASURE)
            {
                if (!Enum.TryParse<ITEM_GRADE_TYPE>(r.Id, out var gradeType) ||
                    gradeType == ITEM_GRADE_TYPE.NONE)
                {
                    return GameResult.Failure(
                        GAME_ERROR_TYPE.INVENTORY_DELTA_ID_EMPTY,
                        $"rewards[{index}] invalid treasure grade id: {r.Id}");
                }
            }

            if (r.Type == REWARD_TYPE.CARD && TB_ITEM_CARD.Get(r.Id) == null)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.ABILITY_ITEM_TABLE_NOT_FOUND,
                    $"rewards[{index}] ITEM_CARD not found: {r.Id}");
            }

            if (r.Type == REWARD_TYPE.MATERIAL && TB_ITEM_MATERIAL.Get(r.Id) == null)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.ABILITY_ITEM_TABLE_NOT_FOUND,
                    $"rewards[{index}] ITEM_MATERIAL not found: {r.Id}");
            }

            if (r.Type == REWARD_TYPE.EQUIP && TB_ITEM_EQUIP.Get(r.Id) == null)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.ABILITY_ITEM_TABLE_NOT_FOUND,
                    $"rewards[{index}] ITEM_EQUIP not found: {r.Id}");
            }

            if (r.Type == REWARD_TYPE.HERO && TB_ITEM_HERO.Get(r.Id) == null)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.ABILITY_ITEM_TABLE_NOT_FOUND,
                    $"rewards[{index}] ITEM_HERO not found: {r.Id}");
            }

            return GameResult.Ok();
        }

        static GameResult<FirstRewardSettings> _loadFirstRewardSettings()
        {
            var setting = Resources.Load<FirstRewardSettings>(FirstRewardSettings.ResourcesPath);
            if (setting == null)
            {
                return GameResult<FirstRewardSettings>.Failure(
                    GAME_ERROR_TYPE.GAME_SERVER_TIME_UNAVAILABLE,
                    $"FirstRewardSettings is not available. expected={FirstRewardSettings.DefaultResourcesAssetPath}");
            }

            return GameResult<FirstRewardSettings>.Success(setting);
        }

        GameResult<RewardData[]> _parseFirstRewardSettings(FirstRewardSettings setting)
        {
            var payload = ((string)setting.InitialRewards)?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(payload))
                return GameResult<RewardData[]>.Success(Array.Empty<RewardData>());

            // AES 복호화
            string json;
            var app = MobileApplication.Instance;
            var cryptoKey = app != null ? app.CryptoKey : string.Empty;
            var cryptoIv = app != null ? app.CryptoIv : string.Empty;
            if (!string.IsNullOrEmpty(cryptoKey) && !string.IsNullOrEmpty(cryptoIv))
            {
                try
                {
                    json = MobileApplication.DecryptJson(payload, cryptoKey, cryptoIv);
                }
                catch (Exception ex)
                {
                    return GameResult<RewardData[]>.Failure(
                        GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                        $"InitialRewards AES decrypt failed: {ex.Message}");
                }
            }
            else
            {
                json = payload;
            }

            if (string.IsNullOrWhiteSpace(json))
                return GameResult<RewardData[]>.Success(Array.Empty<RewardData>());

            JToken root;
            try
            {
                root = JToken.Parse(json);
            }
            catch (Exception ex)
            {
                return GameResult<RewardData[]>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"InitialRewards JSON parse failed: {ex.Message}");
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
                return GameResult<RewardData[]>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "InitialRewards must be RewardData[] JSON or {\"rewards\": RewardData[]}.");
            }

            if (rewardsArray.Count == 0)
                return GameResult<RewardData[]>.Success(Array.Empty<RewardData>());

            var rewards = new RewardData[rewardsArray.Count];
            for (var i = 0; i < rewardsArray.Count; i++)
            {
                if (rewardsArray[i] is not JObject rewardObj)
                {
                    return GameResult<RewardData[]>.Failure(
                        GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                        $"InitialRewards[{i}] must be an object.");
                }

                var typeText = (rewardObj.Value<string>("type") ?? string.Empty).Trim();
                if (string.Equals(typeText, "SEASON_PASS", StringComparison.OrdinalIgnoreCase))
                    typeText = "PASS";

                if (!Enum.TryParse(typeText, true, out REWARD_TYPE rewardType))
                {
                    return GameResult<RewardData[]>.Failure(
                        GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                        $"InitialRewards[{i}].type is invalid: {typeText}");
                }

                var id = (rewardObj.Value<string>("id") ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(id))
                {
                    return GameResult<RewardData[]>.Failure(
                        GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                        $"InitialRewards[{i}].id is empty.");
                }

                var amountToken = rewardObj["amount"];
                if (amountToken == null || amountToken.Type != JTokenType.Integer)
                {
                    return GameResult<RewardData[]>.Failure(
                        GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                        $"InitialRewards[{i}].amount must be an integer.");
                }

                var amountLong = amountToken.Value<long>();
                if (amountLong <= 0 || amountLong > int.MaxValue)
                {
                    return GameResult<RewardData[]>.Failure(
                        GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                        $"InitialRewards[{i}].amount must be within 1..{int.MaxValue}.");
                }

                rewards[i] = new RewardData(rewardType, id, (int)amountLong);
            }

            return GameResult<RewardData[]>.Success(rewards);
        }

        static ITEM_HERO resolveFirstSelectedHeroItem(FirstRewardSettings setting)
        {
            if (setting == null || !setting.SelectedHeroUnitId.IsValid())
                return null;

            var selectedHeroUnitId = ((string)setting.SelectedHeroUnitId)?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(selectedHeroUnitId))
                return null;

            var heroRows = TB_ITEM_HERO.GetAll();
            for (var i = 0; i < heroRows.Count; i++)
            {
                var heroRow = heroRows[i];
                if (heroRow == null)
                    continue;

                if (!string.Equals(heroRow.unit_id, selectedHeroUnitId, StringComparison.Ordinal))
                    continue;

                var heroItemId = heroRow.item_id != null ? heroRow.item_id.Trim() : string.Empty;
                if (!string.IsNullOrEmpty(heroItemId))
                    return heroRow;
            }

            Debug.LogWarning(
                $"[RewardManager] FirstRewardSettings.SelectedHeroUnitId has no matching ITEM_HERO row. unitId={selectedHeroUnitId}");
            return null;
        }

        static RewardData[] appendFirstSelectedHeroReward(RewardData[] rewards, string heroItemId)
        {
            return appendReward(rewards, REWARD_TYPE.HERO, heroItemId, 1);
        }

        static RewardData[] appendFirstSelectedHeroEquipRewards(
            RewardData[] rewards,
            IReadOnlyList<FirstSelectedHeroEquipGrant> equipGrants)
        {
            var merged = rewards ?? Array.Empty<RewardData>();
            if (equipGrants == null || equipGrants.Count == 0)
                return merged;

            for (var i = 0; i < equipGrants.Count; i++)
                merged = appendReward(merged, REWARD_TYPE.EQUIP, equipGrants[i].ItemId, 1);

            return merged;
        }

        static RewardData[] appendReward(RewardData[] rewards, REWARD_TYPE rewardType, string rewardId, int amount)
        {
            if (string.IsNullOrWhiteSpace(rewardId) || amount <= 0)
                return rewards ?? Array.Empty<RewardData>();

            var source = rewards ?? Array.Empty<RewardData>();
            for (var i = 0; i < source.Length; i++)
            {
                var reward = source[i];
                if (reward.Type != rewardType
                    || !string.Equals(reward.Id, rewardId, StringComparison.Ordinal))
                    continue;

                var merged = new RewardData[source.Length];
                Array.Copy(source, merged, source.Length);
                merged[i] = new RewardData(reward.Type, reward.Id, reward.Amount + amount);
                return merged;
            }

            var appended = new RewardData[source.Length + 1];
            if (source.Length > 0)
                Array.Copy(source, appended, source.Length);

            appended[^1] = new RewardData(rewardType, rewardId, amount);
            return appended;
        }

        static FirstSelectedHeroEquipGrant[] collectFirstSelectedHeroEquipGrants(ITEM_HERO selectedHeroItem)
        {
            if (selectedHeroItem == null)
                return Array.Empty<FirstSelectedHeroEquipGrant>();

            var list = new List<FirstSelectedHeroEquipGrant>(4);
            appendFirstSelectedHeroEquipGrant(list, selectedHeroItem.initial_slot_00, selectedHeroItem.initial_item_00);
            appendFirstSelectedHeroEquipGrant(list, selectedHeroItem.initial_slot_01, selectedHeroItem.initial_item_01);
            appendFirstSelectedHeroEquipGrant(list, selectedHeroItem.initial_slot_02, selectedHeroItem.initial_item_02);
            appendFirstSelectedHeroEquipGrant(list, selectedHeroItem.initial_slot_03, selectedHeroItem.initial_item_03);
            return list.Count == 0 ? Array.Empty<FirstSelectedHeroEquipGrant>() : list.ToArray();
        }

        static void appendFirstSelectedHeroEquipGrant(
            List<FirstSelectedHeroEquipGrant> list,
            EQUIP_SLOT_TYPE slotType,
            string itemId)
        {
            var normalizedItemId = itemId != null ? itemId.Trim() : string.Empty;
            if (string.IsNullOrEmpty(normalizedItemId))
                return;

            if (slotType == EQUIP_SLOT_TYPE.NONE)
            {
                Debug.LogWarning(
                    $"[RewardManager] ITEM_HERO initial equip item has no slot. itemId={normalizedItemId}");
                return;
            }

            list.Add(new FirstSelectedHeroEquipGrant(slotType, normalizedItemId));
        }

        static Dictionary<string, HashSet<string>> captureOwnedEquipUids(
            InventoryManager inv,
            IReadOnlyList<FirstSelectedHeroEquipGrant> equipGrants)
        {
            var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            if (inv == null || equipGrants == null || equipGrants.Count == 0)
                return result;

            for (var i = 0; i < equipGrants.Count; i++)
            {
                var itemId = equipGrants[i].ItemId;
                if (result.ContainsKey(itemId))
                    continue;

                var currentEquips = inv.GetEquipsByItemId(itemId);
                var knownUids = new HashSet<string>(StringComparer.Ordinal);
                for (var j = 0; j < currentEquips.Count; j++)
                {
                    var equip = currentEquips[j];
                    var itemUid = equip?.ItemUid ?? string.Empty;
                    if (!string.IsNullOrEmpty(itemUid))
                        knownUids.Add(itemUid);
                }

                result[itemId] = knownUids;
            }

            return result;
        }

        static GameResult applyFirstSelectedHeroInitialEquips(
            InventoryManager inv,
            string heroItemId,
            IReadOnlyList<FirstSelectedHeroEquipGrant> equipGrants,
            IReadOnlyDictionary<string, HashSet<string>> previousEquipUidsByItemId)
        {
            if (inv == null || string.IsNullOrWhiteSpace(heroItemId) || equipGrants == null || equipGrants.Count == 0)
                return GameResult.Ok();

            var grantedEquipUidsByItemId = new Dictionary<string, Queue<string>>(StringComparer.Ordinal);
            for (var i = 0; i < equipGrants.Count; i++)
            {
                var itemId = equipGrants[i].ItemId;
                if (grantedEquipUidsByItemId.ContainsKey(itemId))
                    continue;

                previousEquipUidsByItemId.TryGetValue(itemId, out var previousEquipUids);
                previousEquipUids ??= new HashSet<string>(StringComparer.Ordinal);

                var grantedUids = new Queue<string>();
                var currentEquips = inv.GetEquipsByItemId(itemId);
                for (var j = 0; j < currentEquips.Count; j++)
                {
                    var equip = currentEquips[j];
                    var itemUid = equip?.ItemUid ?? string.Empty;
                    if (string.IsNullOrEmpty(itemUid) || previousEquipUids.Contains(itemUid))
                        continue;

                    grantedUids.Enqueue(itemUid);
                }

                grantedEquipUidsByItemId[itemId] = grantedUids;
            }

            for (var i = 0; i < equipGrants.Count; i++)
            {
                var equipGrant = equipGrants[i];
                if (!grantedEquipUidsByItemId.TryGetValue(equipGrant.ItemId, out var grantedUids)
                    || grantedUids.Count == 0)
                {
                    return GameResult.Failure(
                        GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                        $"RewardManager.applyFirstSelectedHeroInitialEquips: granted equip uid not found. heroItemId={heroItemId}, equipItemId={equipGrant.ItemId}, slotType={equipGrant.SlotType}");
                }

                var equipUid = grantedUids.Dequeue();
                var setEquip = inv.SetHeroEquip(heroItemId, equipGrant.SlotType, equipUid);
                if (setEquip.IsFailure)
                    return GameResult.Failure(setEquip.Error!);
            }

            return GameResult.Ok();
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
