using System.Threading;
using System.Threading.Tasks;


namespace Devian
{
    /// <summary>
    /// Apple iCloud cloud save client.
    /// Delegates all operations to AppleLoginController (iCloud Key-Value Storage).
    /// </summary>
    public sealed class AppleCloudSaveClient : CloudSaveClientApple
    {
        public bool IsAvailable => LoginManager.Instance._getAppleLoginController().IsAvailable;


        public Task<CloudSaveResult> SignInIfNeededAsync(CancellationToken ct)
        {
#if UNITY_IOS && !UNITY_EDITOR
            return LoginManager.Instance._getAppleLoginController().SignInIfNeededAsync(ct);
#else
            return Task.FromResult(CloudSaveResult.NotAvailable);
#endif
        }


        public Task<(CloudSaveResult result, CloudSavePayload payload)> LoadAsync(string slot, CancellationToken ct)
        {
#if UNITY_IOS && !UNITY_EDITOR
            return LoginManager.Instance._getAppleLoginController().LoadAsync(slot, ct);
#else
            return Task.FromResult<(CloudSaveResult, CloudSavePayload)>((CloudSaveResult.NotAvailable, null));
#endif
        }


        public Task<CloudSaveResult> SaveAsync(string slot, CloudSavePayload payload, CancellationToken ct)
        {
#if UNITY_IOS && !UNITY_EDITOR
            return LoginManager.Instance._getAppleLoginController().SaveAsync(slot, payload, ct);
#else
            return Task.FromResult(CloudSaveResult.NotAvailable);
#endif
        }


        public Task<CloudSaveResult> DeleteAsync(string slot, CancellationToken ct)
        {
#if UNITY_IOS && !UNITY_EDITOR
            return LoginManager.Instance._getAppleLoginController().DeleteAsync(slot, ct);
#else
            return Task.FromResult(CloudSaveResult.NotAvailable);
#endif
        }
    }
}
