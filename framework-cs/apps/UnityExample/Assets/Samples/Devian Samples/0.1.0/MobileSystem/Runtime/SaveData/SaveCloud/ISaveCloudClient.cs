using System.Threading;
using System.Threading.Tasks;


namespace Devian
{
    public interface ISaveCloudClient
    {
        bool IsAvailable { get; }

        Task<SaveCloudResult> SignInIfNeededAsync(CancellationToken ct);

        Task<(SaveCloudResult result, SaveCloudPayload payload)> LoadAsync(string slot, CancellationToken ct);

        Task<SaveCloudResult> SaveAsync(string slot, SaveCloudPayload payload, CancellationToken ct);

        Task<SaveCloudResult> DeleteAsync(string slot, CancellationToken ct);
    }
}
