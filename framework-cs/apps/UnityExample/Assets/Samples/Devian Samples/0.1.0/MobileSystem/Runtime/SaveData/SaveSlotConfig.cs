using System;
using System.Collections.Generic;
using UnityEngine;
using Devian.Domain.Common;

namespace Devian
{
    [Serializable]
    public sealed class SaveSlotConfig
    {
        [Header("Security (Shared)")]
        public bool useEncryption = true;
        public CString keyBase64;
        public CString ivBase64;

        [Header("Slots (Shared)")]
        public List<SaveSlot> slots = new();

        public List<string> GetLocalSlotKeys()
        {
            var keys = new List<string>(slots?.Count ?? 0);
            if (slots == null) return keys;

            for (var i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s == null) continue;

                var key = s.slotKey;
                if (string.IsNullOrWhiteSpace(key)) continue;

                var filename = s.filename?.Replace('\\', '/').Trim();
                if (string.IsNullOrWhiteSpace(filename)) continue;

                if (!SaveDataManager.IsValidJsonFilename(filename, out _)) continue;

                keys.Add(key);
            }
            return keys;
        }

        public List<string> GetCloudSlotKeys()
        {
            var keys = new List<string>(slots?.Count ?? 0);
            if (slots == null) return keys;

            for (var i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s == null) continue;

                var key = s.slotKey;
                if (string.IsNullOrWhiteSpace(key)) continue;

                var cloudSlot = s.cloudSlot?.Trim();
                if (string.IsNullOrWhiteSpace(cloudSlot)) continue;

                keys.Add(key);
            }
            return keys;
        }

        public bool TryResolveLocalFilename(string slotKey, out string filename)
        {
            filename = null;
            if (slots == null) return false;

            for (var i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s == null) continue;
                if (!string.Equals(s.slotKey, slotKey, StringComparison.Ordinal)) continue;

                var fn = s.filename?.Replace('\\', '/').Trim();
                if (string.IsNullOrWhiteSpace(fn)) return false;

                filename = fn;
                return true;
            }
            return false;
        }

        public bool TryResolveCloudSlot(string slotKey, out string cloudSlot)
        {
            cloudSlot = null;
            if (slots == null) return false;

            for (var i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s == null) continue;
                if (!string.Equals(s.slotKey, slotKey, StringComparison.Ordinal)) continue;

                cloudSlot = s.cloudSlot;
                return !string.IsNullOrWhiteSpace(cloudSlot);
            }
            return false;
        }

        public void GetKeyIvBase64(out string keyBase64Out, out string ivBase64Out)
        {
            keyBase64Out = keyBase64.Value;
            ivBase64Out = ivBase64.Value;
        }

        public CommonResult<bool> SetKeyIvBase64(string keyBase64In, string ivBase64In)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(keyBase64In) || string.IsNullOrWhiteSpace(ivBase64In))
                {
                    return CommonResult<bool>.Failure(CommonErrorType.LOCALSAVE_KEYIV, "Key/IV is not set.");
                }

                var key = Convert.FromBase64String(keyBase64In);
                var iv = Convert.FromBase64String(ivBase64In);

                if (key.Length != 32)
                {
                    return CommonResult<bool>.Failure(CommonErrorType.LOCALSAVE_KEYIV, "Key must be 32 bytes (AES-256).");
                }

                if (iv.Length != 16)
                {
                    return CommonResult<bool>.Failure(CommonErrorType.LOCALSAVE_KEYIV, "IV must be 16 bytes.");
                }

                keyBase64.Value = keyBase64In;
                ivBase64.Value = ivBase64In;

                return CommonResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return CommonResult<bool>.Failure(CommonErrorType.LOCALSAVE_KEYIV, ex.Message);
            }
        }

        public void ClearKeyIv()
        {
            keyBase64 = default;
            ivBase64 = default;
        }

        public bool TryGetKeyIv(out byte[] key, out byte[] iv, out string error)
        {
            try
            {
                var keyB64 = keyBase64.Value;
                var ivB64 = ivBase64.Value;
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
    }
}
