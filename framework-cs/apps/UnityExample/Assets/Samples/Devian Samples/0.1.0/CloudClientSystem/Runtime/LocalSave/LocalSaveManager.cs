using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Devian.Domain.Common;

namespace Devian
{
    [Serializable]
    public sealed class LocalSaveSlot
    {
        public string slotKey;
        public string filename;
    }

    public enum LocalSaveRoot
    {
        PersistentData,
        TemporaryCache
    }

    public sealed class LocalSaveManager : CompoSingleton<LocalSaveManager>
    {
        private const int SchemaVersion = 1;

        [Header("Storage")]
        [SerializeField] private LocalSaveRoot _root = LocalSaveRoot.PersistentData;

        [Header("Security")]
        [SerializeField] private bool _useEncryption = true;
        [SerializeField] private CString _keyBase64;
        [SerializeField] private CString _ivBase64;

        [Header("Slots")]
        [SerializeField] private List<LocalSaveSlot> _slots = new();

        protected override void Awake()
        {
            base.Awake();
        }

        public void Configure(
            LocalSaveRoot? root = null,
            bool? useEncryption = null,
            List<LocalSaveSlot> slots = null)
        {
            if (root.HasValue) _root = root.Value;
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
                    return CoreResult<bool>.Failure(CommonErrorType.LOCALSAVE_KEYIV, "Key/IV is not set.");
                }

                var key = Convert.FromBase64String(keyBase64);
                var iv = Convert.FromBase64String(ivBase64);

                if (key.Length != 32)
                {
                    return CoreResult<bool>.Failure(CommonErrorType.LOCALSAVE_KEYIV, "Key must be 32 bytes (AES-256).");
                }

                if (iv.Length != 16)
                {
                    return CoreResult<bool>.Failure(CommonErrorType.LOCALSAVE_KEYIV, "IV must be 16 bytes.");
                }

                _keyBase64.Value = keyBase64;
                _ivBase64.Value = ivBase64;

                return CoreResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return CoreResult<bool>.Failure(CommonErrorType.LOCALSAVE_KEYIV, ex.Message);
            }
        }

        public void ClearKeyIv()
        {
            _keyBase64 = default;
            _ivBase64 = default;
        }

        public System.Collections.Generic.List<string> GetSlotKeys()
        {
            var keys = new System.Collections.Generic.List<string>(_slots.Count);
            for (var i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                if (s == null) continue;

                var key = s.slotKey;
                if (string.IsNullOrWhiteSpace(key)) continue;

                var filename = s.filename?.Replace('\\', '/').Trim();
                if (string.IsNullOrWhiteSpace(filename)) continue;

                if (!IsValidJsonFilename(filename, out _)) continue;

                keys.Add(key);
            }
            return keys;
        }

        public CoreResult<LocalSavePayload> LoadRecord(string slot)
        {
            if (string.IsNullOrWhiteSpace(slot))
            {
                return CoreResult<LocalSavePayload>.Failure(CommonErrorType.LOCALSAVE_SLOT_EMPTY, "Slot is empty.");
            }

            if (!TryResolveFilename(slot, out var filename))
            {
                return CoreResult<LocalSavePayload>.Failure(CommonErrorType.LOCALSAVE_SLOT_MISSING, $"Slot '{slot}' not configured.");
            }

            if (!IsValidJsonFilename(filename, out var fnError))
            {
                return CoreResult<LocalSavePayload>.Failure(CommonErrorType.LOCALSAVE_FILENAME_INVALID, fnError);
            }

            var loaded = LocalSaveFileStore.Read(GetRootPath(), filename);
            if (loaded.IsFailure)
            {
                return CoreResult<LocalSavePayload>.Failure(loaded.Error!);
            }

            var save = loaded.Value;
            if (save == null)
            {
                return CoreResult<LocalSavePayload>.Success(null);
            }

            var expected = LocalSaveCrypto.ComputeSha256Base64(save.payload);
            if (!string.Equals(expected, save.checksum, StringComparison.Ordinal))
            {
                return CoreResult<LocalSavePayload>.Failure(CommonErrorType.LOCALSAVE_CHECKSUM, "Checksum mismatch.");
            }

            byte[] key = null;
            byte[] iv = null;

            if (_useEncryption && !TryGetKeyIv(out key, out iv, out var keyError))
            {
                return CoreResult<LocalSavePayload>.Failure(CommonErrorType.LOCALSAVE_KEYIV, keyError);
            }

            var plain = _useEncryption
                ? Crypto.DecryptAes(save.payload, key, iv)
                : save.payload;

            // Return record with decrypted payload (so Sync can compare utcTime + use payload directly)
            return CoreResult<LocalSavePayload>.Success(
                new LocalSavePayload(save.version, save.updateTime, save.utcTime, plain, save.checksum));
        }

        public System.Threading.Tasks.Task<CoreResult<LocalSavePayload>> LoadRecordAsync(
            string slot,
            System.Threading.CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
            {
                return System.Threading.Tasks.Task.FromResult(
                    CoreResult<LocalSavePayload>.Failure(CommonErrorType.LOCALSAVE_CANCELLED, "Cancelled."));
            }

            return System.Threading.Tasks.Task.FromResult(LoadRecord(slot));
        }

        public CoreResult<bool> Save(string slot, string payload)
        {
            if (string.IsNullOrWhiteSpace(slot))
            {
                return CoreResult<bool>.Failure(CommonErrorType.LOCALSAVE_SLOT_EMPTY, "Slot is empty.");
            }

            if (!TryResolveFilename(slot, out var filename))
            {
                return CoreResult<bool>.Failure(CommonErrorType.LOCALSAVE_SLOT_MISSING, $"Slot '{slot}' not configured.");
            }

            if (!IsValidJsonFilename(filename, out var fnError))
            {
                return CoreResult<bool>.Failure(CommonErrorType.LOCALSAVE_FILENAME_INVALID, fnError);
            }

            var plain = payload ?? string.Empty;

            byte[] key = null;
            byte[] iv = null;

            if (_useEncryption && !TryGetKeyIv(out key, out iv, out var keyError))
            {
                return CoreResult<bool>.Failure(CommonErrorType.LOCALSAVE_KEYIV, keyError);
            }

            var cipher = _useEncryption
                ? Crypto.EncryptAes(plain, key, iv)
                : plain;

            var checksum = LocalSaveCrypto.ComputeSha256Base64(cipher);

            var save = new LocalSavePayload(
                SchemaVersion,
                NowUpdateTime(),
                NowUtcTime(),
                cipher,
                checksum
            );

            var write = LocalSaveFileStore.WriteAtomic(GetRootPath(), filename, save);
            return write.IsSuccess
                ? CoreResult<bool>.Success(true)
                : CoreResult<bool>.Failure(write.Error!);
        }

        public CoreResult<string> LoadPayload(string slot)
        {
            if (string.IsNullOrWhiteSpace(slot))
            {
                return CoreResult<string>.Failure(CommonErrorType.LOCALSAVE_SLOT_EMPTY, "Slot is empty.");
            }

            if (!TryResolveFilename(slot, out var filename))
            {
                return CoreResult<string>.Failure(CommonErrorType.LOCALSAVE_SLOT_MISSING, $"Slot '{slot}' not configured.");
            }

            if (!IsValidJsonFilename(filename, out var fnError))
            {
                return CoreResult<string>.Failure(CommonErrorType.LOCALSAVE_FILENAME_INVALID, fnError);
            }

            var loaded = LocalSaveFileStore.Read(GetRootPath(), filename);
            if (loaded.IsFailure)
            {
                return CoreResult<string>.Failure(loaded.Error!);
            }

            var save = loaded.Value;
            if (save == null)
            {
                return CoreResult<string>.Success(null);
            }

            var expected = LocalSaveCrypto.ComputeSha256Base64(save.payload);
            if (!string.Equals(expected, save.checksum, StringComparison.Ordinal))
            {
                return CoreResult<string>.Failure(CommonErrorType.LOCALSAVE_CHECKSUM, "Checksum mismatch.");
            }

            byte[] key = null;
            byte[] iv = null;

            if (_useEncryption && !TryGetKeyIv(out key, out iv, out var keyError))
            {
                return CoreResult<string>.Failure(CommonErrorType.LOCALSAVE_KEYIV, keyError);
            }

            var plain = _useEncryption
                ? Crypto.DecryptAes(save.payload, key, iv)
                : save.payload;

            return CoreResult<string>.Success(plain);
        }

        public System.Threading.Tasks.Task<CoreResult<bool>> SaveAsync(
            string slot,
            string payload,
            System.Threading.CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
            {
                return System.Threading.Tasks.Task.FromResult(
                    CoreResult<bool>.Failure(CommonErrorType.LOCALSAVE_CANCELLED, "Cancelled."));
            }

            // Calls the existing synchronous Save (validations remain in one place)
            return System.Threading.Tasks.Task.FromResult(Save(slot, payload));
        }

        public System.Threading.Tasks.Task<CoreResult<string>> LoadPayloadAsync(
            string slot,
            System.Threading.CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
            {
                return System.Threading.Tasks.Task.FromResult(
                    CoreResult<string>.Failure(CommonErrorType.LOCALSAVE_CANCELLED, "Cancelled."));
            }

            return System.Threading.Tasks.Task.FromResult(LoadPayload(slot));
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

        private string GetRootPath()
        {
            return _root == LocalSaveRoot.PersistentData
                ? Application.persistentDataPath
                : Application.temporaryCachePath;
        }

        private bool TryResolveFilename(string slot, out string filename)
        {
            for (var i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                if (s == null) continue;

                if (string.Equals(s.slotKey, slot, StringComparison.Ordinal))
                {
                    filename = s.filename?.Replace('\\', '/').Trim();
                    return !string.IsNullOrWhiteSpace(filename);
                }
            }

            filename = null;
            return false;
        }

        private static bool IsValidJsonFilename(string filename, out string error)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                error = "Filename is empty.";
                return false;
            }

            // Basic traversal guard
            if (filename.Contains(".."))
            {
                error = "Filename must not contain '..'.";
                return false;
            }

            if (!filename.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                error = "Filename must end with .json";
                return false;
            }

            error = null;
            return true;
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
#endif
    }
}
