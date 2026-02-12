using System.Threading;
using System.Threading.Tasks;


namespace Devian
{
    /// <summary>
    /// Apple iCloud cloud save client.
    /// Option A: Stub only (NotAvailable). Real iCloud implementation is deferred.
    /// </summary>
    public sealed class AppleCloudSaveClient : ICloudSaveClient
    {
        public bool IsAvailable => false;


        public Task<CloudSaveResult> SignInIfNeededAsync(CancellationToken ct)
        {
            return Task.FromResult(CloudSaveResult.NotAvailable);
        }


        public Task<(CloudSaveResult result, CloudSavePayload payload)> LoadAsync(string slot, CancellationToken ct)
        {
            return Task.FromResult<(CloudSaveResult, CloudSavePayload)>((CloudSaveResult.NotAvailable, null));
        }


        public Task<CloudSaveResult> SaveAsync(string slot, CloudSavePayload payload, CancellationToken ct)
        {
            return Task.FromResult(CloudSaveResult.NotAvailable);
        }


        public Task<CloudSaveResult> DeleteAsync(string slot, CancellationToken ct)
        {
            return Task.FromResult(CloudSaveResult.NotAvailable);
        }
    }
}
