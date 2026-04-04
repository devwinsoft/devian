using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Common;
using Devian.Domain.Game;


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


#if UNITY_IOS && !UNITY_EDITOR
        public async Task<GameResult> SaveAsync(string slot, SaveCloudPayload payload, CancellationToken ct)
        {
            var result = await AccountManager.Instance._getAccountLoginApple().SaveAsync(slot, payload, ct);
            return mapSaveResult(result);
        }
#else
        public Task<GameResult> SaveAsync(string slot, SaveCloudPayload payload, CancellationToken ct)
        {
            return Task.FromResult(GameResult.Failure(
                GAME_ERROR_TYPE.CLOUDSAVE_CONNECTION_FAILED,
                "Cloud save is not available on this platform."));
        }
#endif


        public Task<SaveCloudResult> DeleteAsync(string slot, CancellationToken ct)
        {
#if UNITY_IOS && !UNITY_EDITOR
            return AccountManager.Instance._getAccountLoginApple().DeleteAsync(slot, ct);
#else
            return Task.FromResult(SaveCloudResult.NotAvailable);
#endif
        }

        private static GameResult mapSaveResult(SaveCloudResult result)
        {
            return result == SaveCloudResult.Success
                ? GameResult.Ok()
                : GameResult.Failure(GAME_ERROR_TYPE.CLOUDSAVE_CONNECTION_FAILED, $"Cloud save failed: {result}");
        }
    }
}
