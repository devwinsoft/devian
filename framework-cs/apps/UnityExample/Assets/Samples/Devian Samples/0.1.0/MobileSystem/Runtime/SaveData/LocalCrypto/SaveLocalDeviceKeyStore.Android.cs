#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using UnityEngine;

namespace Devian
{
    internal static partial class SaveLocalDeviceKeyStore
    {
        private static partial void TryGetOrCreateKeyIvPlatform(
            out byte[] key, out byte[] iv, out string error, out bool result)
        {
            try
            {
                // Android Plugin: Keystore AES-GCM non-exportable KEK wraps 48-byte DEK.
                var bytes = AndroidKeystoreBridge.GetOrCreateSecret48();
                if (!TrySplitKeyIv(bytes, out key, out iv, out error))
                {
                    result = false;
                    return;
                }

                result = true;
            }
            catch (Exception ex)
            {
                key = null;
                iv = null;
                error = $"Android keystore failed: {ex.Message}";
                result = false;
            }
        }
    }

    /// <summary>
    /// JNI bridge to Android native Keystore plugin.
    /// Requires com.devian.crypto.DevianKeystore Java class in Plugins/Android.
    /// </summary>
    internal static class AndroidKeystoreBridge
    {
        private const string ClassName = "com.devian.crypto.DevianKeystore";

        internal static byte[] GetOrCreateSecret48()
        {
            using var cls = new AndroidJavaClass(ClassName);
            // Use sbyte[] to avoid Unity JNI obsolete warnings:
            //   "AndroidJNIHelper.GetSignature: using Byte parameters is obsolete, use SByte parameters instead"
            //   "AndroidJNIHelper: converting from Byte array is obsolete, use SByte array instead"
            var signed = cls.CallStatic<sbyte[]>("getOrCreateSecret48");
            if (signed == null) return null;
            var unsigned = new byte[signed.Length];
            Buffer.BlockCopy(signed, 0, unsigned, 0, signed.Length);
            return unsigned;
        }
    }
}
#endif
