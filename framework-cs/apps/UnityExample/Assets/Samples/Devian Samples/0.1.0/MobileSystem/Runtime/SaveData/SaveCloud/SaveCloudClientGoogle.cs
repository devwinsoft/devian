using System.Threading;
using System.Threading.Tasks;


namespace Devian
{
    /// <summary>
    /// Google Play Games Saved Games (cloud save) client.
    /// Delegates all operations to AccountLoginGpgs (Reflection-based GPGS).
    /// </summary>
    public sealed class SaveCloudClientGoogle : ISaveCloudClient
    {
        public bool IsAvailable => AccountManager.Instance._getAccountLoginGpgs().IsAvailable;

        // ───── ISaveCloudClient ─────

        public Task<SaveCloudResult> SignInIfNeededAsync(CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return AccountManager.Instance._getAccountLoginGpgs().SignInIfNeededAsync(ct);
#else
            return Task.FromResult(SaveCloudResult.NotAvailable);
#endif
        }

        public Task<(SaveCloudResult result, SaveCloudPayload payload)> LoadAsync(
            string slot, CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return AccountManager.Instance._getAccountLoginGpgs().LoadAsync(slot, ct);
#else
            return Task.FromResult((SaveCloudResult.NotAvailable, (SaveCloudPayload)null));
#endif
        }

        public Task<SaveCloudResult> SaveAsync(
            string slot, SaveCloudPayload payload, CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return AccountManager.Instance._getAccountLoginGpgs().SaveAsync(slot, payload, ct);
#else
            return Task.FromResult(SaveCloudResult.NotAvailable);
#endif
        }

        public Task<SaveCloudResult> DeleteAsync(string slot, CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return AccountManager.Instance._getAccountLoginGpgs().DeleteAsync(slot, ct);
#else
            return Task.FromResult(SaveCloudResult.NotAvailable);
#endif
        }
    }
}
