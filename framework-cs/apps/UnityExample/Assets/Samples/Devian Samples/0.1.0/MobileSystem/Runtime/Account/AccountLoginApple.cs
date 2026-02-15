using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Devian.Domain.Common;


namespace Devian
{
    /// <summary>
    /// Apple platform manager.
    /// - iCloud Key-Value Storage for SaveCloud payloads.
    /// - Apple Sign-in stub (Reflection-based, requires AppleAuth plugin on iOS).
    /// Works only on iOS runtime. Other platforms return NotAvailable.
    /// </summary>
    public sealed class AccountLoginApple
    {
        public bool IsAvailable => isAvailable();

        // ───── Sign-in ─────

        public Task<SaveCloudResult> SignInIfNeededAsync(CancellationToken ct)
        {
#if UNITY_IOS && !UNITY_EDITOR
            return Task.FromResult(IsAvailable ? SaveCloudResult.Success : SaveCloudResult.NotAvailable);
#else
            return Task.FromResult(SaveCloudResult.NotAvailable);
#endif
        }

        public Task<CommonResult<LoginCredential>> SignInAsync(CancellationToken ct)
        {
#if UNITY_IOS && !UNITY_EDITOR
            return Task.FromResult(CommonResult<LoginCredential>.Failure(CommonErrorType.LOGIN_APPLE_MISSING_TOKEN,
                "Apple Sign-in is not yet implemented in AccountLoginApple. Use LoginAsync(LoginType, LoginCredential, CancellationToken) with caller-provided credential."));
#else
            return Task.FromResult(CommonResult<LoginCredential>.Failure(CommonErrorType.LOGIN_APPLE_MISSING_TOKEN,
                "Apple Sign-in is not available on this platform."));
#endif
        }

        public void SignOut() { }

        // ───── Cloud Save (iCloud Key-Value) ─────

        public Task<(SaveCloudResult result, SaveCloudPayload payload)> LoadAsync(
            string slot, CancellationToken ct)
        {
#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                if (!IsAvailable) return Task.FromResult((SaveCloudResult.NotAvailable, (SaveCloudPayload)null));
                if (string.IsNullOrWhiteSpace(slot)) return Task.FromResult((SaveCloudResult.FatalFailure, (SaveCloudPayload)null));

                var key = toKey(slot);
                var json = UnityEngine.iOS.iCloud.GetString(key);
                if (string.IsNullOrWhiteSpace(json))
                    return Task.FromResult((SaveCloudResult.NotFound, (SaveCloudPayload)null));

                var payload = JsonUtility.FromJson<SaveCloudPayload>(json);
                return Task.FromResult((SaveCloudResult.Success, payload));
            }
            catch
            {
                return Task.FromResult((SaveCloudResult.FatalFailure, (SaveCloudPayload)null));
            }
#else
            return Task.FromResult((SaveCloudResult.NotAvailable, (SaveCloudPayload)null));
#endif
        }

        public Task<SaveCloudResult> SaveAsync(string slot, SaveCloudPayload payload, CancellationToken ct)
        {
#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                if (!IsAvailable) return Task.FromResult(SaveCloudResult.NotAvailable);
                if (string.IsNullOrWhiteSpace(slot) || payload == null) return Task.FromResult(SaveCloudResult.FatalFailure);

                var key = toKey(slot);
                var json = JsonUtility.ToJson(payload);

                UnityEngine.iOS.iCloud.SetString(key, json);
                UnityEngine.iOS.iCloud.Save();

                return Task.FromResult(SaveCloudResult.Success);
            }
            catch
            {
                return Task.FromResult(SaveCloudResult.FatalFailure);
            }
#else
            return Task.FromResult(SaveCloudResult.NotAvailable);
#endif
        }

        public Task<SaveCloudResult> DeleteAsync(string slot, CancellationToken ct)
        {
#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                if (!IsAvailable) return Task.FromResult(SaveCloudResult.NotAvailable);
                if (string.IsNullOrWhiteSpace(slot)) return Task.FromResult(SaveCloudResult.FatalFailure);

                var key = toKey(slot);
                UnityEngine.iOS.iCloud.SetString(key, string.Empty);
                UnityEngine.iOS.iCloud.Save();

                return Task.FromResult(SaveCloudResult.Success);
            }
            catch
            {
                return Task.FromResult(SaveCloudResult.FatalFailure);
            }
#else
            return Task.FromResult(SaveCloudResult.NotAvailable);
#endif
        }

        // ───── Helpers ─────

        private bool isAvailable()
        {
#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                return true;
            }
            catch
            {
                return false;
            }
#else
            return false;
#endif
        }

        private static string toKey(string slot)
        {
            return $"devian_cloudsave_{slot}";
        }
    }
}
