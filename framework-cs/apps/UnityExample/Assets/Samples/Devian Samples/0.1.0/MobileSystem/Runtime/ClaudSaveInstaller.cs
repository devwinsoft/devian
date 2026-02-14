using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Common;


namespace Devian
{
    public static class ClaudSaveInstaller
    {
        /// <summary>
        /// Common Editor/Android/iOS entry:
        /// - Editor: inject CloudSaveClient so CloudSave init does not depend on GPGS / platform sign-in.
        /// - Android: use CloudSaveManager default client selection (GPGS via GpgsLoginController).
        /// - iOS: use CloudSaveManager default client selection (iCloud via AppleLoginController).
        /// </summary>
        public static Task<CoreResult<CloudSaveResult>> InitializeAsync(CancellationToken ct)
        {
#if UNITY_EDITOR
            // Editor: inject Firebase cloud save so CloudSave init does not depend on GPGS / platform sign-in.
            CloudSaveManager.Instance.Configure(client: new CloudSaveClient());
#elif UNITY_IOS
            // iOS uses CloudSaveManager default client selection (iCloud via AppleCloudSaveClient).
            // Do not inject a client here.
#elif UNITY_ANDROID
            // Android uses CloudSaveManager default client creation (GPGS + activation).
            // Do not inject a client here.
#else
            // Other platforms: keep existing dev flow (default selection).
#endif
            return CloudSaveManager.Instance.InitializeAsync(ct);
        }


        /// <summary>
        /// Common entry + optional configuration (slots/encryption).
        /// Client selection remains platform-branch.
        /// </summary>
        public static Task<CoreResult<CloudSaveResult>> InitializeAsync(
            List<CloudSaveSlot> slots,
            bool useEncryption,
            CancellationToken ct)
        {
            CloudSaveManager.Instance.Configure(
                client: null,
                useEncryption: useEncryption,
                slots: slots);


            return InitializeAsync(ct);
        }
    }
}
