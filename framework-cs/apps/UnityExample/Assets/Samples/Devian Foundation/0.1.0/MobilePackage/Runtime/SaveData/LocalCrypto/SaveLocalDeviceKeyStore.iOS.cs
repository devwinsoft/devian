#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;

namespace Devian
{
    internal static partial class SaveLocalDeviceKeyStore
    {
        private static partial void TryGetOrCreateKeyIvPlatform(
            out byte[] key, out byte[] iv, out string error, out bool result)
        {
            try
            {
                var len = DevianKeychain_GetOrCreateSecret48(out var ptr);
                if (len != 48 || ptr == IntPtr.Zero)
                {
                    key = null;
                    iv = null;
                    error = "iOS keychain returned invalid secret.";
                    result = false;
                    return;
                }

                var bytes = new byte[48];
                Marshal.Copy(ptr, bytes, 0, 48);
                DevianKeychain_Free(ptr);

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
                error = $"iOS keychain failed: {ex.Message}";
                result = false;
            }
        }

        [DllImport("__Internal")]
        private static extern int DevianKeychain_GetOrCreateSecret48(out IntPtr outPtr);

        [DllImport("__Internal")]
        private static extern void DevianKeychain_Free(IntPtr ptr);
    }
}
#endif
