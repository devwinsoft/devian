using System.Threading;
using System.Threading.Tasks;


namespace Devian
{
    public interface CloudSaveClientApple
    {
        bool IsAvailable { get; }

        Task<CloudSaveResult> SignInIfNeededAsync(CancellationToken ct);

        Task<(CloudSaveResult result, CloudSavePayload payload)> LoadAsync(string slot, CancellationToken ct);

        Task<CloudSaveResult> SaveAsync(string slot, CloudSavePayload payload, CancellationToken ct);

        Task<CloudSaveResult> DeleteAsync(string slot, CancellationToken ct);
    }
}
