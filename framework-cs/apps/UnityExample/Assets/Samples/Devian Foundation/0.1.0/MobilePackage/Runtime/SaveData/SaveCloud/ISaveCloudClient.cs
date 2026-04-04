using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Common;
using Devian.Domain.Game;


namespace Devian
{
    public interface ISaveCloudClient
    {
        bool IsAvailable { get; }

        Task<SaveCloudResult> SignInIfNeededAsync(CancellationToken ct);

        Task<(SaveCloudResult result, SaveCloudPayload payload)> LoadAsync(string slot, CancellationToken ct);

        Task<GameResult> SaveAsync(string slot, SaveCloudPayload payload, CancellationToken ct);

        Task<SaveCloudResult> DeleteAsync(string slot, CancellationToken ct);
    }
}
