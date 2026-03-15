using System;
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

        CInt _maxStamina;
        CInt _staminaIntervalSeconds;

        public InventoryStorage Storage => _storage;
        public int MaxStamina => _maxStamina;
        public int StaminaIntervalSeconds => _staminaIntervalSeconds;

        // ── Message API ──

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

        // ── Apply API ──

        public void ApplyCurrency(CURRENCY_TYPE currencyType, long amount)
        {
            _storage.Wallet.TryAdd(currencyType, amount);
        }

        public void ApplyEquip(string equipId, int amount)
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

        public void ApplyCard(string cardId, int amount)
        {
            var existing = _storage.GetCard(cardId);
            if (existing != null)
            {
                existing.AddAmount(amount);
            }
            else
            {
                var table = TB_ITEM_CARD.Get(cardId);
                var ability = new AbilityCard();
                if (table != null)
                    ability.Init(table);

                _storage.AddCard(cardId, ability);
                ability.AddAmount(amount);
            }
        }

        public void ApplyHero(string heroId, int amount)
        {
            var existing = _storage.GetHero(heroId);
            if (existing != null)
            {
                existing.AddStat(STAT_TYPE.UNIT_AMOUNT, amount);
            }
            else
            {
                var table = TB_UNIT_HERO.Get(heroId);
                var ability = new AbilityUnitHero();
                if (table != null)
                    ability.Init(table);

                _storage.AddHero(heroId, ability);
                ability.AddStat(STAT_TYPE.UNIT_AMOUNT, amount);
            }
        }

        public void ApplyRental(string rentalId)
        {
            var nowUtcMs = RemoteDataManager.ServerNowUtcMs;
            var currentExpiryUtcMs = _storage.GetRentalExpiry(rentalId);
            var baseUtcMs = currentExpiryUtcMs > nowUtcMs ? currentExpiryUtcMs : nowUtcMs;
            _storage.SetRental(rentalId, baseUtcMs + DefaultRentalDurationMs);
        }

        public void ApplyTreasure(TREASURE_GRADE_TYPE gradeType, int amount)
        {
            _storage.AddTreasure(gradeType, amount);
        }

        public void SetPassOwnership(string passId, bool owned)
        {
            if (_storage.SetPass(passId, owned))
                _messageTrigger.Notify(MESSAGE_INVENTORY_TYPE.PASS_CHANGED, passId, owned);
        }

        public void RemovePassOwnership(string passId)
        {
            if (_storage.RemovePass(passId))
                _messageTrigger.Notify(MESSAGE_INVENTORY_TYPE.PASS_CHANGED, passId, false);
        }

        // ── Revoke API ──

        public void RevokeCurrency(CURRENCY_TYPE currencyType, long amount)
        {
            _storage.Wallet.TryAdd(currencyType, -amount);
        }

        public void RevokeEquip(string equipId, int amount)
        {
            var equips = _storage.GetEquipsByEquipId(equipId);
            for (var j = 0; j < amount && j < equips.Count; j++)
            {
                if (equips[j] != null && !string.IsNullOrEmpty(equips[j].ItemUid))
                    _storage.RemoveEquip(equips[j].ItemUid);
            }
        }

        public void RevokeCard(string cardId, int amount)
        {
            var card = _storage.GetCard(cardId);
            if (card != null)
                card.AddAmount(-amount);
        }

        public void RevokeHero(string heroId, int amount)
        {
            var hero = _storage.GetHero(heroId);
            if (hero != null)
                hero.AddStat(STAT_TYPE.UNIT_AMOUNT, -amount);
        }

        public void RevokeRental(string rentalId)
        {
            _storage.RemoveRental(rentalId);
        }

        public void RevokeTreasure(TREASURE_GRADE_TYPE gradeType, int amount)
        {
            var current = _storage.GetTreasureCount(gradeType);
            _storage.SetTreasureCount(gradeType, current - amount);
        }

        // ── Query API ──

        public long GetCurrencyAmount(CURRENCY_TYPE currencyType)
        {
            return _storage.Wallet.Get(currencyType);
        }

        public int GetEquipCount(string equipId)
        {
            return _storage.GetEquipsByEquipId(equipId).Count;
        }

        public long GetCardAmount(string cardId)
        {
            var card = _storage.GetCard(cardId);
            return card != null ? card.Amount : 0L;
        }

        public long GetHeroAmount(string heroId)
        {
            var hero = _storage.GetHero(heroId);
            return hero != null ? hero[STAT_TYPE.UNIT_AMOUNT] : 0L;
        }

        public bool HasActiveRental(string rentalId)
        {
            return _storage.HasActiveRental(rentalId);
        }

        public bool HasPass(string passId)
        {
            return _storage.HasPass(passId);
        }

        public int GetTreasureCount(TREASURE_GRADE_TYPE gradeType)
        {
            return _storage.GetTreasureCount(gradeType);
        }

        // ── Stamina ──

        public void Initialize()
        {
            _maxStamina = 30;
            _staminaIntervalSeconds = 300;

            var settings = Resources.Load<InventorySettings>(InventorySettings.ResourcesPath);
            if (settings == null)
            {
                Debug.LogWarning($"[InventoryManager] InventorySettings not found. Using defaults. expected={InventorySettings.DefaultResourcesAssetPath}");
                return;
            }

            var payload = ((string)settings.SettingsPayload)?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(payload))
                return;

            var app = MobileApplication.Instance;
            var cryptoKey = app != null ? app.CryptoKey : string.Empty;
            var cryptoIv = app != null ? app.CryptoIv : string.Empty;

            string json;
            if (!string.IsNullOrEmpty(cryptoKey) && !string.IsNullOrEmpty(cryptoIv))
            {
                try
                {
                    json = MobileApplication.DecryptJson(payload, cryptoKey, cryptoIv);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[InventoryManager] InventorySettings AES decrypt failed: {ex.Message}");
                    return;
                }
            }
            else
            {
                json = payload;
            }

            if (string.IsNullOrWhiteSpace(json))
                return;

            try
            {
                var obj = JObject.Parse(json);
                _maxStamina = obj.Value<int?>("maxStamina") ?? 30;
                _staminaIntervalSeconds = obj.Value<int?>("staminaIntervalSeconds") ?? 300;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InventoryManager] InventorySettings JSON parse failed: {ex.Message}");
            }
        }

        public void UpdateStamina()
        {
            int maxStamina = _maxStamina;
            int intervalSeconds = _staminaIntervalSeconds;

            if (maxStamina <= 0 || intervalSeconds <= 0)
                return;

            var currentStamina = _storage.Wallet.Get(CURRENCY_TYPE.STAMINA);

            // stamina >= max → 회복 불필요, 타임스탬프 무의미
            if (currentStamina >= maxStamina)
                return;

            var nowUtcMs = RemoteDataManager.ServerNowUtcMs;
            var lastUpdateUtcMs = _storage.LastStaminaUpdateUtcMs;

            // 추적 시작: stamina < max인데 타임스탬프 없음 → now 기록
            if (lastUpdateUtcMs <= 0L)
            {
                _storage.LastStaminaUpdateUtcMs = nowUtcMs;
                return;
            }

            var elapsedMs = nowUtcMs - lastUpdateUtcMs;
            if (elapsedMs <= 0L)
                return;

            var intervalMs = (long)intervalSeconds * 1000L;
            var recoveryCount = elapsedMs / intervalMs;
            if (recoveryCount <= 0L)
                return;

            var actualRecovery = Math.Min(recoveryCount, maxStamina - currentStamina);
            if (actualRecovery > 0L)
                _storage.Wallet.TryAdd(CURRENCY_TYPE.STAMINA, actualRecovery);

            // 회복 후 max 도달 → 타임스탬프 클리어 (추적 종료)
            if (currentStamina + actualRecovery >= maxStamina)
            {
                _storage.LastStaminaUpdateUtcMs = 0L;
                return;
            }

            // 아직 max 미만 → 잔여 시간 보존
            var remainderMs = elapsedMs % intervalMs;
            _storage.LastStaminaUpdateUtcMs = nowUtcMs - remainderMs;
        }
    }
}
