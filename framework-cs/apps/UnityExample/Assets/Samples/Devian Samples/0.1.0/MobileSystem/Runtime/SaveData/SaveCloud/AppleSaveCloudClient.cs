using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Common;


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


        public async Task<CommonResult> SaveAsync(string slot, SaveCloudPayload payload, CancellationToken ct)
        {
#if UNITY_IOS && !UNITY_EDITOR
            var result = await AccountManager.Instance._getAccountLoginApple().SaveAsync(slot, payload, ct);
            return mapSaveResult(result);
#else
            return CommonResult.Failure(CommonErrorType.CLOUDSAVE_CONNECTION_FAILED,
                "Cloud save is not available on this platform.");
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

        private static CommonResult mapSaveResult(SaveCloudResult result)
        {
            return result == SaveCloudResult.Success
                ? CommonResult.Ok()
                : CommonResult.Failure(CommonErrorType.CLOUDSAVE_CONNECTION_FAILED, $"Cloud save failed: {result}");
        }
    }
}
