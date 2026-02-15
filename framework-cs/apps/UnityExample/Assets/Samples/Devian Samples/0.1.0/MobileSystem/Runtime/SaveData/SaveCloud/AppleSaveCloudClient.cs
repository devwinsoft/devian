using System.Threading;
using System.Threading.Tasks;


namespace Devian
{
    /// <summary>
    /// Apple iCloud cloud save client.
    /// Delegates all operations to AccountLoginApple (iCloud Key-Value Storage).
    /// </summary>
    public sealed class AppleSaveCloudClient : ISaveCloudClient
    {
        public bool IsAvailable => AccountManager.Instance._getAccountLoginApple().IsAvailable;


        public Task<SaveCloudResult> SignInIfNeededAsync(CancellationToken ct)
        {
#if UNITY_IOS && !UNITY_EDITOR
            return AccountManager.Instance._getAccountLoginApple().SignInIfNeededAsync(ct);
#else
            return Task.FromResult(SaveCloudResult.NotAvailable);
#endif
        }


        public Task<(SaveCloudResult result, SaveCloudPayload payload)> LoadAsync(string slot, CancellationToken ct)
        {
#if UNITY_IOS && !UNITY_EDITOR
            return AccountManager.Instance._getAccountLoginApple().LoadAsync(slot, ct);
#else
            return Task.FromResult<(SaveCloudResult, SaveCloudPayload)>((SaveCloudResult.NotAvailable, null));
#endif
        }


        public Task<SaveCloudResult> SaveAsync(string slot, SaveCloudPayload payload, CancellationToken ct)
        {
#if UNITY_IOS && !UNITY_EDITOR
            return AccountManager.Instance._getAccountLoginApple().SaveAsync(slot, payload, ct);
#else
            return Task.FromResult(SaveCloudResult.NotAvailable);
#endif
        }


        public Task<SaveCloudResult> DeleteAsync(string slot, CancellationToken ct)
        {
#if UNITY_IOS && !UNITY_EDITOR
            return AccountManager.Instance._getAccountLoginApple().DeleteAsync(slot, ct);
#else
            return Task.FromResult(SaveCloudResult.NotAvailable);
#endif
        }
    }
}
