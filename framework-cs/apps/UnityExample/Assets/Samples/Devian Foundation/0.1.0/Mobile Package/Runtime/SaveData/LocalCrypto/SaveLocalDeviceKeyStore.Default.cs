#if UNITY_EDITOR || (!UNITY_ANDROID && !UNITY_IOS)
namespace Devian
{
    internal static partial class SaveLocalDeviceKeyStore
    {
        // Default implementation for Editor/unsupported platforms.
        // This satisfies the partial method requirement when Android/iOS implementations are excluded by defines.
        private static partial void TryGetOrCreateKeyIvPlatform(
            out byte[] key, out byte[] iv, out string error, out bool result)
        {
            key = null;
            iv = null;
            error = "Device-bound keystore/keychain is not available on this platform.";
            result = false;
        }
    }
}
#endif
