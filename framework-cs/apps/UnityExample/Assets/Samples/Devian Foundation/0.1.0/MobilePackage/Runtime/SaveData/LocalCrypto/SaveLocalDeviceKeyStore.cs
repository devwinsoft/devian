using System;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Device-bound key store for Local Save encryption.
    /// Provides KEK/DEK model: KEK lives in OS secure storage (non-exportable),
    /// DEK (key 32B + iv 16B = 48B) is wrapped by KEK and stored locally.
    /// </summary>
    internal static partial class SaveLocalDeviceKeyStore
    {
        // PlayerPrefs key for wrapped DEK blob (base64).
        // Loss on app uninstall is acceptable per Data Loss Policy.
        private const string PrefsKey = "Devian.SaveLocal.DeviceKeyIv";

        internal static bool TryGetOrCreateKeyIv(out byte[] key, out byte[] iv, out string error)
        {
#if UNITY_EDITOR
            // Editor does not support device-bound keystore/keychain.
            // SaveDataManager handles Editor path separately (uses shared key/iv).
            key = null;
            iv = null;
            error = "Editor does not support device-bound keystore/keychain.";
            return false;
#else
            return TryGetOrCreateKeyIvPlatform(out key, out iv, out error);
#endif
        }

        // Platform-specific implementation (partial method in .Android.cs / .iOS.cs)
        private static partial void TryGetOrCreateKeyIvPlatform(
            out byte[] key, out byte[] iv, out string error, out bool result);

        private static bool TryGetOrCreateKeyIvPlatform(out byte[] key, out byte[] iv, out string error)
        {
            TryGetOrCreateKeyIvPlatform(out key, out iv, out error, out var result);
            return result;
        }

        internal static bool TrySplitKeyIv(byte[] keyIv48, out byte[] key, out byte[] iv, out string error)
        {
            if (keyIv48 == null || keyIv48.Length != 48)
            {
                key = null;
                iv = null;
                error = "Invalid key/iv blob. Expected 48 bytes (32 key + 16 iv).";
                return false;
            }

            key = new byte[32];
            iv = new byte[16];
            Buffer.BlockCopy(keyIv48, 0, key, 0, 32);
            Buffer.BlockCopy(keyIv48, 32, iv, 0, 16);
            error = null;
            return true;
        }

        internal static byte[] JoinKeyIv(byte[] key, byte[] iv)
        {
            var buf = new byte[48];
            Buffer.BlockCopy(key, 0, buf, 0, 32);
            Buffer.BlockCopy(iv, 0, buf, 32, 16);
            return buf;
        }
    }
}
