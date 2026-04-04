using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Game;

namespace Devian
{
    /// <summary>
    /// Push 플랫폼 Provider 인터페이스.
    /// PushManager 내부에서만 호출된다.
    /// </summary>
    internal interface IPushPlatformProvider
    {
        Task<GameResult> RequestPermissionAsync(CancellationToken ct);
        Task<GameResult<string>> GetTokenAsync(CancellationToken ct);
        Task<GameResult> SubscribeTopicAsync(string topicId, CancellationToken ct);
        Task<GameResult> UnsubscribeTopicAsync(string topicId, CancellationToken ct);
        Task<GameResult> ScheduleLocalNotificationAsync(LocalNotificationData data, CancellationToken ct);
        Task<GameResult> CancelLocalNotificationAsync(string notificationId, CancellationToken ct);
        Task<GameResult> CancelAllLocalNotificationsAsync(CancellationToken ct);
    }
}
