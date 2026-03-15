using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
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
        // ── Crypto (AES-256-CBC) for FirstRewardSettings ──

        [SerializeField] CString _initialRewardsCryptoKey;
        [SerializeField] CString _initialRewardsCryptoIv;

        public string InitialRewardsCryptoKey => _initialRewardsCryptoKey;
        public string InitialRewardsCryptoIv => _initialRewardsCryptoIv;

        public static string EncryptInitialRewardsJson(string plainJson, string keyBase64, string ivBase64)
        {
            if (string.IsNullOrEmpty(plainJson))
                return string.Empty;

            using var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = Convert.FromBase64String(keyBase64);
            aes.IV = Convert.FromBase64String(ivBase64);

            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plainJson);
            var encrypted = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            return Convert.ToBase64String(encrypted);
        }

        public static string DecryptInitialRewardsJson(string encryptedBase64, string keyBase64, string ivBase64)
        {
            if (string.IsNullOrEmpty(encryptedBase64))
                return string.Empty;

            using var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = Convert.FromBase64String(keyBase64);
            aes.IV = Convert.FromBase64String(ivBase64);

            using var decryptor = aes.CreateDecryptor();
            var encryptedBytes = Convert.FromBase64String(encryptedBase64);
            var decrypted = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
            return Encoding.UTF8.GetString(decrypted);
        }

        // ── Apply RewardGroup ──

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

        // ── Apply RewardDatas (type switch + validation) ──

        public CommonResult ApplyRewardDatas(RewardData[] rewards)
        {
            if (rewards == null)
                return CommonResult.Failure(COMMON_ERROR_TYPE.INVENTORY_DELTAS_NULL, "rewards is null");

            if (rewards.Length == 0)
                return CommonResult.Ok();

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
                    inv.ApplyCurrency(currencyType, r.Amount);
                }
                else if (r.Type == REWARD_TYPE.CARD)
                {
                    inv.ApplyCard(r.Id, (int)r.Amount);
                }
                else if (r.Type == REWARD_TYPE.EQUIP)
                {
                    inv.ApplyEquip(r.Id, (int)r.Amount);
                }
                else if (r.Type == REWARD_TYPE.HERO)
                {
                    inv.ApplyHero(r.Id, (int)r.Amount);
                }
                else if (r.Type == REWARD_TYPE.RENTAL)
                {
                    inv.ApplyRental(r.Id);
                }
                else if (r.Type == REWARD_TYPE.PASS)
                {
                    inv.SetPassOwnership(r.Id, true);
                }
                else if (r.Type == REWARD_TYPE.TREASURE)
                {
                    var gradeType = (TREASURE_GRADE_TYPE)Enum.Parse(typeof(TREASURE_GRADE_TYPE), r.Id);
                    inv.ApplyTreasure(gradeType, (int)r.Amount);
                }
            }

            return CommonResult.Ok();
        }

        // ── Revoke RewardDatas ──

        public CommonResult RevokeRewardDatas(RewardData[] rewards)
        {
            if (rewards == null)
                return CommonResult.Failure(COMMON_ERROR_TYPE.INVENTORY_DELTAS_NULL, "rewards is null");

            if (rewards.Length == 0)
                return CommonResult.Ok();

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
                        return CommonResult.Failure(
                            COMMON_ERROR_TYPE.INVENTORY_REFUND_INSUFFICIENT,
                            $"rewards[{i}] insufficient currency. id={r.Id} need={r.Amount} have={balance}");
                    }
                }
                else if (r.Type == REWARD_TYPE.CARD)
                {
                    var amount = inv.GetCardAmount(r.Id);
                    if (amount < r.Amount)
                    {
                        return CommonResult.Failure(
                            COMMON_ERROR_TYPE.INVENTORY_REFUND_INSUFFICIENT,
                            $"rewards[{i}] insufficient card amount. id={r.Id} need={r.Amount} have={amount}");
                    }
                }
                else if (r.Type == REWARD_TYPE.EQUIP)
                {
                    var count = inv.GetEquipCount(r.Id);
                    if (count < r.Amount)
                    {
                        return CommonResult.Failure(
                            COMMON_ERROR_TYPE.INVENTORY_REFUND_INSUFFICIENT,
                            $"rewards[{i}] insufficient equip count. id={r.Id} need={r.Amount} have={count}");
                    }
                }
                else if (r.Type == REWARD_TYPE.HERO)
                {
                    var amount = inv.GetHeroAmount(r.Id);
                    if (amount < r.Amount)
                    {
                        return CommonResult.Failure(
                            COMMON_ERROR_TYPE.INVENTORY_REFUND_INSUFFICIENT,
                            $"rewards[{i}] insufficient hero amount. id={r.Id} need={r.Amount} have={amount}");
                    }
                }
                else if (r.Type == REWARD_TYPE.RENTAL)
                {
                    if (!inv.HasActiveRental(r.Id))
                    {
                        return CommonResult.Failure(
                            COMMON_ERROR_TYPE.INVENTORY_REFUND_INSUFFICIENT,
                            $"rewards[{i}] rental not active. id={r.Id}");
                    }
                }
                else if (r.Type == REWARD_TYPE.PASS)
                {
                    if (!inv.HasPass(r.Id))
                    {
                        return CommonResult.Failure(
                            COMMON_ERROR_TYPE.INVENTORY_REFUND_INSUFFICIENT,
                            $"rewards[{i}] pass not owned. id={r.Id}");
                    }
                }
                else if (r.Type == REWARD_TYPE.TREASURE)
                {
                    var gradeType = (TREASURE_GRADE_TYPE)Enum.Parse(typeof(TREASURE_GRADE_TYPE), r.Id);
                    var chestCount = inv.GetTreasureCount(gradeType);
                    if (chestCount < r.Amount)
                    {
                        return CommonResult.Failure(
                            COMMON_ERROR_TYPE.INVENTORY_REFUND_INSUFFICIENT,
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
                    inv.RevokeCurrency(currencyType, r.Amount);
                }
                else if (r.Type == REWARD_TYPE.CARD)
                {
                    inv.RevokeCard(r.Id, (int)r.Amount);
                }
                else if (r.Type == REWARD_TYPE.EQUIP)
                {
                    inv.RevokeEquip(r.Id, (int)r.Amount);
                }
                else if (r.Type == REWARD_TYPE.HERO)
                {
                    inv.RevokeHero(r.Id, (int)r.Amount);
                }
                else if (r.Type == REWARD_TYPE.RENTAL)
                {
                    inv.RevokeRental(r.Id);
                }
                else if (r.Type == REWARD_TYPE.PASS)
                {
                    inv.RemovePassOwnership(r.Id);
                }
                else if (r.Type == REWARD_TYPE.TREASURE)
                {
                    var gradeType = (TREASURE_GRADE_TYPE)Enum.Parse(typeof(TREASURE_GRADE_TYPE), r.Id);
                    inv.RevokeTreasure(gradeType, (int)r.Amount);
                }
            }

            return CommonResult.Ok();
        }

        // ── Revoke Partial ──

        public CommonResult RevokeRewardDatasPartial(RewardData[] rewards)
        {
            if (rewards == null)
                return CommonResult.Failure(COMMON_ERROR_TYPE.INVENTORY_DELTAS_NULL, "rewards is null");

            if (rewards.Length == 0)
                return CommonResult.Ok();

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
                        inv.RevokeCurrency(currencyType, clampedAmount);
                }
                else if (r.Type == REWARD_TYPE.CARD)
                {
                    var have = inv.GetCardAmount(r.Id);
                    var clampedAmount = (int)Math.Min(r.Amount, have);
                    if (clampedAmount > 0)
                        inv.RevokeCard(r.Id, clampedAmount);
                }
                else if (r.Type == REWARD_TYPE.EQUIP)
                {
                    var count = inv.GetEquipCount(r.Id);
                    var clampedAmount = (int)Math.Min(r.Amount, count);
                    if (clampedAmount > 0)
                        inv.RevokeEquip(r.Id, clampedAmount);
                }
                else if (r.Type == REWARD_TYPE.HERO)
                {
                    var have = inv.GetHeroAmount(r.Id);
                    var clampedAmount = (int)Math.Min(r.Amount, have);
                    if (clampedAmount > 0)
                        inv.RevokeHero(r.Id, clampedAmount);
                }
                else if (r.Type == REWARD_TYPE.RENTAL)
                {
                    if (inv.HasActiveRental(r.Id))
                        inv.RevokeRental(r.Id);
                }
                else if (r.Type == REWARD_TYPE.PASS)
                {
                    if (inv.HasPass(r.Id))
                        inv.RemovePassOwnership(r.Id);
                }
                else if (r.Type == REWARD_TYPE.TREASURE)
                {
                    var gradeType = (TREASURE_GRADE_TYPE)Enum.Parse(typeof(TREASURE_GRADE_TYPE), r.Id);
                    var have = inv.GetTreasureCount(gradeType);
                    var clampedAmount = (int)Math.Min(r.Amount, have);
                    if (clampedAmount > 0)
                        inv.RevokeTreasure(gradeType, clampedAmount);
                }
            }

            return CommonResult.Ok();
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
                var gradeType = (TREASURE_GRADE_TYPE)Enum.Parse(typeof(TREASURE_GRADE_TYPE), id);
                return inv.GetTreasureCount(gradeType);
            }

            return 0L;
        }

        // ── FirstInitAsync ──

        public async Task<CommonResult> FirstInitAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var parse = _parseFirstRewardSettings();
            if (parse.IsFailure)
                return CommonResult.Failure(parse.Error!);

            var rewards = parse.Value ?? Array.Empty<RewardData>();
            if (rewards.Length == 0)
                return CommonResult.Ok();

            var apply = ApplyRewardDatas(rewards);
            if (apply.IsFailure)
                return CommonResult.Failure(apply.Error!);

            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            return CommonResult.Ok();
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

        // ── Private ──

        static CommonResult _validateRewardData(RewardData r, int index)
        {
            if (r.Type != REWARD_TYPE.CARD && r.Type != REWARD_TYPE.CURRENCY &&
                r.Type != REWARD_TYPE.EQUIP && r.Type != REWARD_TYPE.HERO &&
                r.Type != REWARD_TYPE.RENTAL && r.Type != REWARD_TYPE.PASS &&
                r.Type != REWARD_TYPE.TREASURE)
                return CommonResult.Failure(COMMON_ERROR_TYPE.INVENTORY_DELTA_TYPE_INVALID,
                    $"rewards[{index}] invalid type: {r.Type}");

            if (string.IsNullOrWhiteSpace(r.Id))
                return CommonResult.Failure(COMMON_ERROR_TYPE.INVENTORY_DELTA_ID_EMPTY,
                    $"rewards[{index}] id is empty");

            if (r.Amount < 0)
                return CommonResult.Failure(COMMON_ERROR_TYPE.INVENTORY_DELTA_AMOUNT_NEGATIVE,
                    $"rewards[{index}] amount is negative: {r.Amount}");

            if (r.Type == REWARD_TYPE.CURRENCY)
            {
                if (!Enum.TryParse<CURRENCY_TYPE>(r.Id, out var currencyType) ||
                    currencyType == CURRENCY_TYPE.ADS ||
                    currencyType == CURRENCY_TYPE.FREE ||
                    currencyType == CURRENCY_TYPE.JEWEL)
                {
                    return CommonResult.Failure(
                        COMMON_ERROR_TYPE.INVENTORY_DELTA_ID_EMPTY,
                        $"rewards[{index}] invalid currency id: {r.Id}");
                }
            }

            if (r.Type == REWARD_TYPE.TREASURE)
            {
                if (!Enum.TryParse<TREASURE_GRADE_TYPE>(r.Id, out var gradeType) ||
                    gradeType == TREASURE_GRADE_TYPE.NONE)
                {
                    return CommonResult.Failure(
                        COMMON_ERROR_TYPE.INVENTORY_DELTA_ID_EMPTY,
                        $"rewards[{index}] invalid treasure grade id: {r.Id}");
                }
            }

            return CommonResult.Ok();
        }

        CommonResult<RewardData[]> _parseFirstRewardSettings()
        {
            var setting = Resources.Load<FirstRewardSettings>(FirstRewardSettings.ResourcesPath);
            if (setting == null)
            {
                return CommonResult<RewardData[]>.Failure(
                    COMMON_ERROR_TYPE.COMMON_SERVER,
                    $"FirstRewardSettings is not available. expected={FirstRewardSettings.DefaultResourcesAssetPath}");
            }

            var payload = ((string)setting.InitialRewards)?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(payload))
                return CommonResult<RewardData[]>.Success(Array.Empty<RewardData>());

            // AES 복호화
            string json;
            if (!string.IsNullOrEmpty(_initialRewardsCryptoKey) && !string.IsNullOrEmpty(_initialRewardsCryptoIv))
            {
                try
                {
                    json = DecryptInitialRewardsJson(payload, _initialRewardsCryptoKey, _initialRewardsCryptoIv);
                }
                catch (Exception ex)
                {
                    return CommonResult<RewardData[]>.Failure(
                        COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                        $"InitialRewards AES decrypt failed: {ex.Message}");
                }
            }
            else
            {
                json = payload;
            }

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
                return CommonResult<RewardData[]>.Failure(
                    COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    "InitialRewards must be RewardData[] JSON or {\"rewards\": RewardData[]}.");
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
                        $"InitialRewards[{i}] must be an object.");
                }

                var typeText = (rewardObj.Value<string>("type") ?? string.Empty).Trim();
                if (string.Equals(typeText, "SEASON_PASS", StringComparison.OrdinalIgnoreCase))
                    typeText = "PASS";

                if (!Enum.TryParse(typeText, true, out REWARD_TYPE rewardType))
                {
                    return CommonResult<RewardData[]>.Failure(
                        COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                        $"InitialRewards[{i}].type is invalid: {typeText}");
                }

                var id = (rewardObj.Value<string>("id") ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(id))
                {
                    return CommonResult<RewardData[]>.Failure(
                        COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                        $"InitialRewards[{i}].id is empty.");
                }

                var amountToken = rewardObj["amount"];
                if (amountToken == null || amountToken.Type != JTokenType.Integer)
                {
                    return CommonResult<RewardData[]>.Failure(
                        COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                        $"InitialRewards[{i}].amount must be an integer.");
                }

                var amountLong = amountToken.Value<long>();
                if (amountLong <= 0 || amountLong > int.MaxValue)
                {
                    return CommonResult<RewardData[]>.Failure(
                        COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                        $"InitialRewards[{i}].amount must be within 1..{int.MaxValue}.");
                }

                rewards[i] = new RewardData(rewardType, id, (int)amountLong);
            }

            return CommonResult<RewardData[]>.Success(rewards);
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
