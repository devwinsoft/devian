using System.Threading;
using System.Threading.Tasks;

namespace Devian
{
    /// <summary>
    /// Samples-only stub (NOT used by default).
    /// The default sample path uses <see cref="GooglePlayGamesCloudSaveClient"/> from Foundation.
    /// Keep this file as a reference for projects that want a custom client implementation.
    /// </summary>
    public sealed class GpgsCloudSaveClient : ICloudSaveClient
    {
        private readonly object _googlePlayService;

        public bool IsAvailable => false;

        public GpgsCloudSaveClient(object googlePlayService)
        {
            _googlePlayService = googlePlayService;
        }

        public Task<CloudSaveResult> SignInIfNeededAsync(CancellationToken ct)
        {
            return Task.FromResult(CloudSaveResult.NotAvailable);
        }

        public Task<(CloudSaveResult result, CloudSavePayload payload)> LoadAsync(
            string slot, CancellationToken ct)
        {
            return Task.FromResult((CloudSaveResult.NotAvailable, (CloudSavePayload)null));
        }

        public Task<CloudSaveResult> SaveAsync(
            string slot, CloudSavePayload payload, CancellationToken ct)
        {
            return Task.FromResult(CloudSaveResult.NotAvailable);
        }

        public Task<CloudSaveResult> DeleteAsync(string slot, CancellationToken ct)
        {
            return Task.FromResult(CloudSaveResult.NotAvailable);
        }
    }
}
