using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Devian.Domain.Common;

namespace Devian
{
    [Serializable]
    public sealed class CloudSaveSlot
    {
        public string slotKey;
        public string cloudSlot;
    }

    public sealed class CloudSaveManager : CompoSingleton<CloudSaveManager>
    {
        private const int SchemaVersion = 1;

        [Header("Security")]
        [SerializeField] private bool _useEncryption;
        [SerializeField] private CString _keyBase64;
        [SerializeField] private CString _ivBase64;

        [Header("Slots")]
        [SerializeField] private List<CloudSaveSlot> _slots = new();

        private ICloudSaveClient _client;

        protected override void Awake()
        {
            base.Awake();
        }

        public void Configure(
            ICloudSaveClient client = null,
            bool? useEncryption = null,
            List<CloudSaveSlot> slots = null)
        {
            if (client != null) _client = client;
            if (useEncryption.HasValue) _useEncryption = useEncryption.Value;
            if (slots != null) _slots = slots;
        }

        public void GetKeyIvBase64(out string keyBase64, out string ivBase64)
        {
            keyBase64 = _keyBase64.Value;
            ivBase64 = _ivBase64.Value;
        }

        public CoreResult<bool> SetKeyIvBase64(string keyBase64, string ivBase64)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(keyBase64) || string.IsNullOrWhiteSpace(ivBase64))
                {
                    return CoreResult<bool>.Failure(ErrorClientType.CLOUDSAVE_KEYIV, "Key/IV is not set.");
                }

                var key = Convert.FromBase64String(keyBase64);
                var iv = Convert.FromBase64String(ivBase64);

                if (key.Length != 32)
                {
                    return CoreResult<bool>.Failure(ErrorClientType.CLOUDSAVE_KEYIV, "Key must be 32 bytes (AES-256).");
                }

                if (iv.Length != 16)
                {
                    return CoreResult<bool>.Failure(ErrorClientType.CLOUDSAVE_KEYIV, "IV must be 16 bytes.");
                }

                _keyBase64.Value = keyBase64;
                _ivBase64.Value = ivBase64;

                return CoreResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return CoreResult<bool>.Failure(ErrorClientType.CLOUDSAVE_KEYIV, ex.Message);
            }
        }

        public void ClearKeyIv()
        {
            _keyBase64 = default;
            _ivBase64 = default;
        }

        public bool IsAvailable => _client != null && _client.IsAvailable;

        /// <summary>
        /// One-line init:
        /// - If client not configured: create default cloud save client by platform.
        ///   - Android: Google Play Games Saved Games (GPGS)
        ///   - iOS: null (must be injected from app/sample layer)
        /// - Then sign-in (or no-op if already signed in).
        /// </summary>
        public Task<CoreResult<CloudSaveResult>> InitializeAsync(CancellationToken ct)
        {
            if (_client == null)
            {
                _client = _createDefaultClient();
            }

            return _signInInternal(ct);
        }

        private static ICloudSaveClient _createDefaultClient()
        {
#if UNITY_IOS && !UNITY_EDITOR
            // iOS uses iCloud (Option A: stub client for now).
            return new AppleCloudSaveClient();
#elif UNITY_ANDROID && !UNITY_EDITOR
            // Android uses GPGS (Saved Games). Best-effort activate when plugin exists.
            _tryActivateGpgsSavedGames();
            return new GoogleCloudSaveClient();
#else
            // Editor/other platforms: keep existing dev flow.
            return new GoogleCloudSaveClient();
#endif
        }

        public Task<CoreResult<CloudSaveResult>> SignInIfNeededAsync(CancellationToken ct)
        {
            if (_client == null)
                return Task.FromResult(
                    CoreResult<CloudSaveResult>.Failure(ErrorClientType.CLOUDSAVE_NOCLIENT, "Client not configured."));

            return _signInInternal(ct);
        }

        public Task<CoreResult<bool>> SaveAsync(string slot, string payload, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(slot))
                return Task.FromResult(
                    CoreResult<bool>.Failure(ErrorClientType.CLOUDSAVE_SLOT_EMPTY, "Slot is empty."));

            if (_client == null)
                return Task.FromResult(
                    CoreResult<bool>.Failure(ErrorClientType.CLOUDSAVE_NOCLIENT, "Client not configured."));

            if (!TryResolveCloudSlot(slot, out var cloudSlot))
                return Task.FromResult(
                    CoreResult<bool>.Failure(ErrorClientType.CLOUDSAVE_SLOT_MISSING, $"Slot '{slot}' not configured."));

            if (!IsLikelyJson(payload))
                return Task.FromResult(
                    CoreResult<bool>.Failure(ErrorClientType.CLOUDSAVE_PAYLOAD_INVALID,
                        "Payload must be JSON (object or array)."));

            return _saveInternal(cloudSlot, payload, ct);
        }

        public Task<CoreResult<string>> LoadPayloadAsync(string slot, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(slot))
                return Task.FromResult(
                    CoreResult<string>.Failure(ErrorClientType.CLOUDSAVE_SLOT_EMPTY, "Slot is empty."));

            if (_client == null)
                return Task.FromResult(
                    CoreResult<string>.Failure(ErrorClientType.CLOUDSAVE_NOCLIENT, "Client not configured."));

            if (!TryResolveCloudSlot(slot, out var cloudSlot))
                return Task.FromResult(
                    CoreResult<string>.Failure(ErrorClientType.CLOUDSAVE_SLOT_MISSING, $"Slot '{slot}' not configured."));

            return _loadInternal(cloudSlot, ct);
        }

        public Task<CoreResult<bool>> DeleteAsync(string slot, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(slot))
                return Task.FromResult(
                    CoreResult<bool>.Failure(ErrorClientType.CLOUDSAVE_SLOT_EMPTY, "Slot is empty."));

            if (_client == null)
                return Task.FromResult(
                    CoreResult<bool>.Failure(ErrorClientType.CLOUDSAVE_NOCLIENT, "Client not configured."));

            if (!TryResolveCloudSlot(slot, out var cloudSlot))
                return Task.FromResult(
                    CoreResult<bool>.Failure(ErrorClientType.CLOUDSAVE_SLOT_MISSING, $"Slot '{slot}' not configured."));

            return _deleteInternal(cloudSlot, ct);
        }

        private async Task<CoreResult<CloudSaveResult>> _signInInternal(CancellationToken ct)
        {
            var r = await _client.SignInIfNeededAsync(ct);
            return r == CloudSaveResult.Success
                ? CoreResult<CloudSaveResult>.Success(r)
                : CoreResult<CloudSaveResult>.Failure(ErrorClientType.CLOUDSAVE_SIGNIN, $"Sign-in failed: {r}");
        }

        private const string _gpgsConfigBuilderTypeName =
            "GooglePlayGames.BasicApi.PlayGamesClientConfiguration+Builder, GooglePlayGames";
        private const string _playGamesPlatformTypeName =
            "GooglePlayGames.PlayGamesPlatform, GooglePlayGames";

        private static void _tryActivateGpgsSavedGames()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                var builderType = Type.GetType(_gpgsConfigBuilderTypeName);
                var platformType = Type.GetType(_playGamesPlatformTypeName);

                if (builderType == null || platformType == null) return;

                var builder = Activator.CreateInstance(builderType);
                if (builder == null) return;

                builderType.GetMethod("EnableSavedGames", Type.EmptyTypes)
                    ?.Invoke(builder, null);

                var config = builderType.GetMethod("Build", Type.EmptyTypes)
                    ?.Invoke(builder, null);
                if (config == null) return;

                MethodInfo init = null;
                var methods = platformType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                for (var i = 0; i < methods.Length; i++)
                {
                    var m = methods[i];
                    if (!string.Equals(m.Name, "InitializeInstance", StringComparison.Ordinal)) continue;
                    var ps = m.GetParameters();
                    if (ps.Length == 1)
                    {
                        init = m;
                        break;
                    }
                }

                init?.Invoke(null, new[] { config });

                platformType.GetMethod("Activate", BindingFlags.Public | BindingFlags.Static)
                    ?.Invoke(null, null);
            }
            catch
            {
                // Best-effort: no throw.
            }
#endif
        }

        private async Task<CoreResult<bool>> _saveInternal(
            string cloudSlot, string payload, CancellationToken ct)
        {
            var plain = payload ?? string.Empty;

            byte[] key = null;
            byte[] iv = null;

            if (_useEncryption && !TryGetKeyIv(out key, out iv, out var keyError))
            {
                return CoreResult<bool>.Failure(ErrorClientType.CLOUDSAVE_KEYIV, keyError);
            }

            var cipher = _useEncryption
                ? Crypto.EncryptAes(plain, key, iv)
                : plain;

            var checksum = CloudSaveCrypto.ComputeSha256Base64(cipher);

            var csPayload = new CloudSavePayload(
                SchemaVersion,
                NowUpdateTime(),
                NowUtcTime(),
                cipher,
                checksum
            );

            var r = await _client.SaveAsync(cloudSlot, csPayload, ct);
            return r == CloudSaveResult.Success
                ? CoreResult<bool>.Success(true)
                : CoreResult<bool>.Failure(ErrorClientType.CLOUDSAVE_SAVE, $"Save failed: {r}");
        }

        private async Task<CoreResult<string>> _loadInternal(
            string cloudSlot, CancellationToken ct)
        {
            var (result, loaded) = await _client.LoadAsync(cloudSlot, ct);
            if (result != CloudSaveResult.Success)
            {
                return CoreResult<string>.Failure(ErrorClientType.CLOUDSAVE_LOAD, $"Load failed: {result}");
            }

            if (loaded == null)
            {
                return CoreResult<string>.Success(null);
            }

            var expected = CloudSaveCrypto.ComputeSha256Base64(loaded.Payload);
            if (!string.IsNullOrEmpty(loaded.Checksum) &&
                !string.Equals(expected, loaded.Checksum, StringComparison.Ordinal))
            {
                return CoreResult<string>.Failure(ErrorClientType.CLOUDSAVE_CHECKSUM, "Checksum mismatch.");
            }

            byte[] key = null;
            byte[] iv = null;

            if (_useEncryption && !TryGetKeyIv(out key, out iv, out var keyError))
            {
                return CoreResult<string>.Failure(ErrorClientType.CLOUDSAVE_KEYIV, keyError);
            }

            var plain = _useEncryption
                ? Crypto.DecryptAes(loaded.Payload, key, iv)
                : loaded.Payload;

            return CoreResult<string>.Success(plain);
        }

        private async Task<CoreResult<bool>> _deleteInternal(
            string cloudSlot, CancellationToken ct)
        {
            var r = await _client.DeleteAsync(cloudSlot, ct);
            return r == CloudSaveResult.Success
                ? CoreResult<bool>.Success(true)
                : CoreResult<bool>.Failure(ErrorClientType.CLOUDSAVE_DELETE, $"Delete failed: {r}");
        }

        private const string UpdateTimeFormat = "yyyyMMdd:HHmmss";

        private static string NowUpdateTime()
        {
            return DateTime.Now.ToString(UpdateTimeFormat, CultureInfo.InvariantCulture);
        }

        private static long NowUtcTime()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        private bool TryResolveCloudSlot(string slot, out string cloudSlot)
        {
            for (var i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                if (s == null) continue;

                if (string.Equals(s.slotKey, slot, StringComparison.Ordinal))
                {
                    cloudSlot = s.cloudSlot;
                    return !string.IsNullOrWhiteSpace(cloudSlot);
                }
            }

            cloudSlot = null;
            return false;
        }

        private static bool IsLikelyJson(string s)
        {
            if (s == null) return true;
            s = s.Trim();
            if (s.Length == 0) return true;

            // Minimal deterministic check without a JSON parser (old Unity safe):
            // Accept object/array only.
            var first = s[0];
            var last = s[s.Length - 1];
            if (first == '{' && last == '}') return true;
            if (first == '[' && last == ']') return true;
            return false;
        }

        private bool TryGetKeyIv(out byte[] key, out byte[] iv, out string error)
        {
            try
            {
                var keyB64 = _keyBase64.Value;
                var ivB64 = _ivBase64.Value;
                if (string.IsNullOrWhiteSpace(keyB64) || string.IsNullOrWhiteSpace(ivB64))
                {
                    key = null;
                    iv = null;
                    error = "Key/IV is not set.";
                    return false;
                }

                key = Convert.FromBase64String(keyB64);
                iv = Convert.FromBase64String(ivB64);

                if (key.Length != 32)
                {
                    error = "Key must be 32 bytes (AES-256).";
                    return false;
                }

                if (iv.Length != 16)
                {
                    error = "IV must be 16 bytes.";
                    return false;
                }

                error = null;
                return true;
            }
            catch (Exception ex)
            {
                key = null;
                iv = null;
                error = ex.Message;
                return false;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Generate Key/IV (AES-256, Base64)")]
        public void GenerateKeyIv()
        {
            _keyBase64.Value = Convert.ToBase64String(Crypto.GenerateKey());
            _ivBase64.Value = Convert.ToBase64String(Crypto.GenerateIv());
        }

        private void OnValidate()
        {
            if (!_useEncryption) return;

            var keyB64 = _keyBase64.Value;
            var ivB64 = _ivBase64.Value;

            if (string.IsNullOrWhiteSpace(keyB64) || string.IsNullOrWhiteSpace(ivB64))
            {
                GenerateKeyIv();
            }
        }
#endif
    }
}
