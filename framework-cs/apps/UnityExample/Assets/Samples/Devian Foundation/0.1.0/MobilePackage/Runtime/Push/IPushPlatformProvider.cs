using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Common;

namespace Devian
{
    /// <summary>
    /// Push 플랫폼 Provider 인터페이스.
    /// PushManager 내부에서만 호출된다.
    /// </summary>
    internal interface IPushPlatformProvider
    {
        Task<CommonResult> RequestPermissionAsync(CancellationToken ct);
        Task<CommonResult<string>> GetTokenAsync(CancellationToken ct);
        Task<CommonResult> SubscribeTopicAsync(string topicId, CancellationToken ct);
        Task<CommonResult> UnsubscribeTopicAsync(string topicId, CancellationToken ct);
        Task<CommonResult> ScheduleLocalNotificationAsync(LocalNotificationData data, CancellationToken ct);
        Task<CommonResult> CancelLocalNotificationAsync(string notificationId, CancellationToken ct);
        Task<CommonResult> CancelAllLocalNotificationsAsync(CancellationToken ct);
    }
}
