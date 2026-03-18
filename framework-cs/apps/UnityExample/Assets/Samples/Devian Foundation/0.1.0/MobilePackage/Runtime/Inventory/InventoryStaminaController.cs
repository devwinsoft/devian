using System;
using Devian.Domain.Game;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Devian
{
    internal sealed class InventoryStaminaController
    {
        CInt _maxStamina;
        CInt _staminaIntervalSeconds;

        public int MaxStamina => _maxStamina;

        public void LoadSettings()
        {
            _maxStamina = 30;
            _staminaIntervalSeconds = 300;

            var settings = Resources.Load<InventorySettings>(InventorySettings.ResourcesPath);
            if (settings == null)
            {
                Debug.LogWarning($"[InventoryStaminaController] InventorySettings not found. Using defaults. expected={InventorySettings.DefaultResourcesAssetPath}");
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
                    Debug.LogError($"[InventoryStaminaController] InventorySettings AES decrypt failed: {ex.Message}");
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
                Debug.LogError($"[InventoryStaminaController] InventorySettings JSON parse failed: {ex.Message}");
            }
        }

        public void RecoverStamina(InventoryStorage storage)
        {
            int maxStamina = _maxStamina;
            int intervalSeconds = _staminaIntervalSeconds;

            if (maxStamina <= 0 || intervalSeconds <= 0)
                return;

            var currentStamina = storage.Wallet.Get(CURRENCY_TYPE.STAMINA);

            // stamina >= max → 회복 불필요, 타임스탬프 무의미
            if (currentStamina >= maxStamina)
                return;

            var nowUtcMs = RemoteDataManager.ServerNowUtcMs;
            var lastUpdateUtcMs = storage.LastStaminaUpdateUtcMs;

            // 추적 시작: stamina < max인데 타임스탬프 없음 → now 기록
            if (lastUpdateUtcMs <= 0L)
            {
                storage.LastStaminaUpdateUtcMs = nowUtcMs;
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
                storage.Wallet.TryAdd(CURRENCY_TYPE.STAMINA, actualRecovery);

            // 회복 후 max 도달 → 타임스탬프 클리어 (추적 종료)
            if (currentStamina + actualRecovery >= maxStamina)
            {
                storage.LastStaminaUpdateUtcMs = 0L;
                return;
            }

            // 아직 max 미만 → 잔여 시간 보존
            var remainderMs = elapsedMs % intervalMs;
            storage.LastStaminaUpdateUtcMs = nowUtcMs - remainderMs;
        }
    }
}
