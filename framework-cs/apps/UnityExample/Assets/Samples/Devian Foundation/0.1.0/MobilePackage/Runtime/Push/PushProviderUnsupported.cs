using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Game;

namespace Devian
{
    /// <summary>
    /// Editor 및 지원하지 않는 플랫폼용 Provider. 즉시 안전 실패를 반환한다.
    /// </summary>
    internal sealed class PushProviderUnsupported : IPushPlatformProvider
    {
        private const string Msg = "Push is not supported on this platform.";

        public Task<GameResult> RequestPermissionAsync(CancellationToken ct)
            => Task.FromResult(GameResult.Failure(GAME_ERROR_TYPE.PUSH_UNSUPPORTED_PLATFORM, Msg));

        public Task<GameResult<string>> GetTokenAsync(CancellationToken ct)
            => Task.FromResult(GameResult<string>.Failure(GAME_ERROR_TYPE.PUSH_UNSUPPORTED_PLATFORM, Msg));

        public Task<GameResult> SubscribeTopicAsync(string topicId, CancellationToken ct)
            => Task.FromResult(GameResult.Failure(GAME_ERROR_TYPE.PUSH_UNSUPPORTED_PLATFORM, Msg));

        public Task<GameResult> UnsubscribeTopicAsync(string topicId, CancellationToken ct)
            => Task.FromResult(GameResult.Failure(GAME_ERROR_TYPE.PUSH_UNSUPPORTED_PLATFORM, Msg));

        public Task<GameResult> ScheduleLocalNotificationAsync(LocalNotificationData data, CancellationToken ct)
            => Task.FromResult(GameResult.Failure(GAME_ERROR_TYPE.PUSH_UNSUPPORTED_PLATFORM, Msg));

        public Task<GameResult> CancelLocalNotificationAsync(string notificationId, CancellationToken ct)
            => Task.FromResult(GameResult.Failure(GAME_ERROR_TYPE.PUSH_UNSUPPORTED_PLATFORM, Msg));

        public Task<GameResult> CancelAllLocalNotificationsAsync(CancellationToken ct)
            => Task.FromResult(GameResult.Failure(GAME_ERROR_TYPE.PUSH_UNSUPPORTED_PLATFORM, Msg));
    }
}
