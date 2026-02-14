using System.Threading;
using System.Threading.Tasks;


namespace Devian
{
    /// <summary>
    /// Google Play Games Saved Games (cloud save) client.
    /// Delegates all operations to GpgsLoginController (Reflection-based GPGS).
    /// </summary>
    public sealed class CloudSaveClientGoogle : CloudSaveClientApple
    {
        public bool IsAvailable => LoginManager.Instance._getGpgsLoginController().IsAvailable;

        // ───── CloudSaveClientApple ─────

        public Task<CloudSaveResult> SignInIfNeededAsync(CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return LoginManager.Instance._getGpgsLoginController().SignInIfNeededAsync(ct);
#else
            return Task.FromResult(CloudSaveResult.NotAvailable);
#endif
        }

        public Task<(CloudSaveResult result, CloudSavePayload payload)> LoadAsync(
            string slot, CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return LoginManager.Instance._getGpgsLoginController().LoadAsync(slot, ct);
#else
            return Task.FromResult((CloudSaveResult.NotAvailable, (CloudSavePayload)null));
#endif
        }

        public Task<CloudSaveResult> SaveAsync(
            string slot, CloudSavePayload payload, CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return LoginManager.Instance._getGpgsLoginController().SaveAsync(slot, payload, ct);
#else
            return Task.FromResult(CloudSaveResult.NotAvailable);
#endif
        }

        public Task<CloudSaveResult> DeleteAsync(string slot, CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return LoginManager.Instance._getGpgsLoginController().DeleteAsync(slot, ct);
#else
            return Task.FromResult(CloudSaveResult.NotAvailable);
#endif
        }
    }
}
