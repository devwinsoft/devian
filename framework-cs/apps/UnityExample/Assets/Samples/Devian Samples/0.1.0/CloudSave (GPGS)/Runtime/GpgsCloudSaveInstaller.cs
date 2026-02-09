using System.Collections.Generic;

namespace Devian
{
    public static class GpgsCloudSaveInstaller
    {
        /// <summary>
        /// Samples-only: configure CloudSaveManager by injecting only the client.
        /// Slots/encryption settings are taken from CloudSaveManager's serialized fields (Inspector).
        /// No login/auth flow here. Caller must ensure the underlying GooglePlayService is ready.
        /// </summary>
        public static void ConfigureCloudSave(object googlePlayService)
        {
            var client = new GpgsCloudSaveClient(googlePlayService);
            CloudSaveManager.Instance.Configure(client: client);
        }

        /// <summary>
        /// Samples-only: create a GPGS-backed ICloudSaveClient and configure CloudSaveManager in one shot.
        /// No login/auth flow here. Caller must ensure the underlying GooglePlayService is ready.
        /// </summary>
        public static void ConfigureCloudSave(
            object googlePlayService,
            List<CloudSaveSlot> slots,
            bool useEncryption)
        {
            var client = new GpgsCloudSaveClient(googlePlayService);
            CloudSaveManager.Instance.Configure(client: client, useEncryption: useEncryption, slots: slots);
        }
    }
}
