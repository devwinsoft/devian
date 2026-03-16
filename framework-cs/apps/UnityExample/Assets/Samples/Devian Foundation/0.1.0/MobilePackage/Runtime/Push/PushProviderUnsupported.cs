using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Common;

namespace Devian
{
    /// <summary>
    /// Editor 및 지원하지 않는 플랫폼용 Provider. 즉시 안전 실패를 반환한다.
    /// </summary>
    internal sealed class PushProviderUnsupported : IPushPlatformProvider
    {
        private const string Msg = "Push is not supported on this platform.";

        public Task<CommonResult> RequestPermissionAsync(CancellationToken ct)
            => Task.FromResult(CommonResult.Failure(COMMON_ERROR_TYPE.PUSH_UNSUPPORTED_PLATFORM, Msg));

        public Task<CommonResult<string>> GetTokenAsync(CancellationToken ct)
            => Task.FromResult(CommonResult<string>.Failure(COMMON_ERROR_TYPE.PUSH_UNSUPPORTED_PLATFORM, Msg));

        public Task<CommonResult> SubscribeTopicAsync(string topicId, CancellationToken ct)
            => Task.FromResult(CommonResult.Failure(COMMON_ERROR_TYPE.PUSH_UNSUPPORTED_PLATFORM, Msg));

        public Task<CommonResult> UnsubscribeTopicAsync(string topicId, CancellationToken ct)
            => Task.FromResult(CommonResult.Failure(COMMON_ERROR_TYPE.PUSH_UNSUPPORTED_PLATFORM, Msg));

        public Task<CommonResult> ScheduleLocalNotificationAsync(LocalNotificationData data, CancellationToken ct)
            => Task.FromResult(CommonResult.Failure(COMMON_ERROR_TYPE.PUSH_UNSUPPORTED_PLATFORM, Msg));

        public Task<CommonResult> CancelLocalNotificationAsync(string notificationId, CancellationToken ct)
            => Task.FromResult(CommonResult.Failure(COMMON_ERROR_TYPE.PUSH_UNSUPPORTED_PLATFORM, Msg));

        public Task<CommonResult> CancelAllLocalNotificationsAsync(CancellationToken ct)
            => Task.FromResult(CommonResult.Failure(COMMON_ERROR_TYPE.PUSH_UNSUPPORTED_PLATFORM, Msg));
    }
}
