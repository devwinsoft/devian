using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Devian.Domain.Common;


namespace Devian
{
    /// <summary>
    /// Apple platform manager.
    /// - iCloud Key-Value Storage for CloudSave payloads.
    /// - Apple Sign-in stub (Reflection-based, requires AppleAuth plugin on iOS).
    /// Works only on iOS runtime. Other platforms return NotAvailable.
    /// </summary>
    public sealed class AppleLoginController
    {
        public bool IsAvailable => isAvailable();

        // ───── Sign-in ─────

        public Task<CloudSaveResult> SignInIfNeededAsync(CancellationToken ct)
        {
#if UNITY_IOS && !UNITY_EDITOR
            return Task.FromResult(IsAvailable ? CloudSaveResult.Success : CloudSaveResult.NotAvailable);
#else
            return Task.FromResult(CloudSaveResult.NotAvailable);
#endif
        }

        public Task<CoreResult<LoginCredential>> SignInAsync(CancellationToken ct)
        {
#if UNITY_IOS && !UNITY_EDITOR
            return Task.FromResult(CoreResult<LoginCredential>.Failure(CommonErrorType.LOGIN_APPLE_MISSING_TOKEN,
                "Apple Sign-in is not yet implemented in AppleLoginController. Use LoginAsync(LoginType, LoginCredential, CancellationToken) with caller-provided credential."));
#else
            return Task.FromResult(CoreResult<LoginCredential>.Failure(CommonErrorType.LOGIN_APPLE_MISSING_TOKEN,
                "Apple Sign-in is not available on this platform."));
#endif
        }

        public void SignOut() { }

        // ───── Cloud Save (iCloud Key-Value) ─────

        public Task<(CloudSaveResult result, CloudSavePayload payload)> LoadAsync(
            string slot, CancellationToken ct)
        {
#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                if (!IsAvailable) return Task.FromResult((CloudSaveResult.NotAvailable, (CloudSavePayload)null));
                if (string.IsNullOrWhiteSpace(slot)) return Task.FromResult((CloudSaveResult.FatalFailure, (CloudSavePayload)null));

                var key = toKey(slot);
                var json = UnityEngine.iOS.iCloud.GetString(key);
                if (string.IsNullOrWhiteSpace(json))
                    return Task.FromResult((CloudSaveResult.NotFound, (CloudSavePayload)null));

                var payload = JsonUtility.FromJson<CloudSavePayload>(json);
                return Task.FromResult((CloudSaveResult.Success, payload));
            }
            catch
            {
                return Task.FromResult((CloudSaveResult.FatalFailure, (CloudSavePayload)null));
            }
#else
            return Task.FromResult((CloudSaveResult.NotAvailable, (CloudSavePayload)null));
#endif
        }

        public Task<CloudSaveResult> SaveAsync(string slot, CloudSavePayload payload, CancellationToken ct)
        {
#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                if (!IsAvailable) return Task.FromResult(CloudSaveResult.NotAvailable);
                if (string.IsNullOrWhiteSpace(slot) || payload == null) return Task.FromResult(CloudSaveResult.FatalFailure);

                var key = toKey(slot);
                var json = JsonUtility.ToJson(payload);

                UnityEngine.iOS.iCloud.SetString(key, json);
                UnityEngine.iOS.iCloud.Save();

                return Task.FromResult(CloudSaveResult.Success);
            }
            catch
            {
                return Task.FromResult(CloudSaveResult.FatalFailure);
            }
#else
            return Task.FromResult(CloudSaveResult.NotAvailable);
#endif
        }

        public Task<CloudSaveResult> DeleteAsync(string slot, CancellationToken ct)
        {
#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                if (!IsAvailable) return Task.FromResult(CloudSaveResult.NotAvailable);
                if (string.IsNullOrWhiteSpace(slot)) return Task.FromResult(CloudSaveResult.FatalFailure);

                var key = toKey(slot);
                UnityEngine.iOS.iCloud.SetString(key, string.Empty);
                UnityEngine.iOS.iCloud.Save();

                return Task.FromResult(CloudSaveResult.Success);
            }
            catch
            {
                return Task.FromResult(CloudSaveResult.FatalFailure);
            }
#else
            return Task.FromResult(CloudSaveResult.NotAvailable);
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
