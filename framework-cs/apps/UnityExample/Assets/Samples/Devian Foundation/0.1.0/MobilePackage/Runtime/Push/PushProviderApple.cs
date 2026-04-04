using System;
using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Game;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR && UNITY_MOBILE_NOTIFICATIONS
using Unity.Notifications.iOS;
#endif

namespace Devian
{
    /// <summary>
    /// iOS(APNs) Push Provider.
    /// Firebase Messaging iOS SDK를 통해 토큰 획득/토픽 구독을 처리한다.
    /// 로컬 알림은 Unity Mobile Notifications 패키지를 사용한다.
    /// </summary>
    internal sealed class PushProviderApple : IPushPlatformProvider
    {
        private const string Tag = nameof(PushProviderApple);

        public async Task<GameResult> RequestPermissionAsync(CancellationToken ct)
        {
#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                // TODO: UNUserNotificationCenter.RequestAuthorization
                // provisional → full 권한 요청 구현
                return GameResult.Ok();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return GameResult.Failure(GAME_ERROR_TYPE.PUSH_PERMISSION_DENIED, ex.Message);
            }
#else
            return GameResult.Failure(GAME_ERROR_TYPE.PUSH_UNSUPPORTED_PLATFORM,
                "Apple push provider is not available on this platform.");
#endif
        }

        public async Task<GameResult<string>> GetTokenAsync(CancellationToken ct)
        {
#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                // Firebase Messaging iOS SDK가 APNs 토큰을 내부 매핑하여 FCM 토큰 반환
                var token = await Firebase.Messaging.FirebaseMessaging.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return GameResult<string>.Failure(
                        GAME_ERROR_TYPE.PUSH_TOKEN_FAILED, "FCM token is empty.");
                }

                return GameResult<string>.Success(token);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return GameResult<string>.Failure(GAME_ERROR_TYPE.PUSH_TOKEN_FAILED, ex.Message);
            }
#else
            return GameResult<string>.Failure(GAME_ERROR_TYPE.PUSH_UNSUPPORTED_PLATFORM,
                "Apple push provider is not available on this platform.");
#endif
        }

        public async Task<GameResult> SubscribeTopicAsync(string topicId, CancellationToken ct)
        {
#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                await Firebase.Messaging.FirebaseMessaging.SubscribeAsync(topicId);
                return GameResult.Ok();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return GameResult.Failure(GAME_ERROR_TYPE.PUSH_TOPIC_SUBSCRIBE_FAILED, ex.Message);
            }
#else
            return GameResult.Failure(GAME_ERROR_TYPE.PUSH_UNSUPPORTED_PLATFORM,
                "Apple push provider is not available on this platform.");
#endif
        }

        public async Task<GameResult> UnsubscribeTopicAsync(string topicId, CancellationToken ct)
        {
#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                await Firebase.Messaging.FirebaseMessaging.UnsubscribeAsync(topicId);
                return GameResult.Ok();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return GameResult.Failure(GAME_ERROR_TYPE.PUSH_TOPIC_UNSUBSCRIBE_FAILED, ex.Message);
            }
#else
            return GameResult.Failure(GAME_ERROR_TYPE.PUSH_UNSUPPORTED_PLATFORM,
                "Apple push provider is not available on this platform.");
#endif
        }

        public async Task<GameResult> ScheduleLocalNotificationAsync(
            LocalNotificationData data, CancellationToken ct)
        {
#if UNITY_IOS && !UNITY_EDITOR && UNITY_MOBILE_NOTIFICATIONS
            try
            {
                // iOS: 아이콘은 시스템이 앱 아이콘을 강제 사용한다. 별도 설정 불가.
                var timeTrigger = new iOSNotificationTimeIntervalTrigger
                {
                    TimeInterval = data.FireAt - DateTime.Now,
                    Repeats = false,
                };

                var notification = new iOSNotification
                {
                    Identifier = data.NotificationId,
                    Title = data.Title,
                    Body = data.Body,
                    Data = data.Payload,
                    ShowInForeground = true,
                    ForegroundPresentationOption =
                        PresentationOption.Alert | PresentationOption.Sound,
                    Trigger = timeTrigger,
                };

                iOSNotificationCenter.ScheduleNotification(notification);
                Debug.Log($"[{Tag}] ScheduleLocalNotification: {data.NotificationId}");
                return GameResult.Ok();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return GameResult.Failure(GAME_ERROR_TYPE.PUSH_NOTIFICATION_SCHEDULE_FAILED, ex.Message);
            }
#else
            return GameResult.Failure(GAME_ERROR_TYPE.PUSH_UNSUPPORTED_PLATFORM,
                "Apple push provider is not available on this platform.");
#endif
        }

        public async Task<GameResult> CancelLocalNotificationAsync(
            string notificationId, CancellationToken ct)
        {
#if UNITY_IOS && !UNITY_EDITOR && UNITY_MOBILE_NOTIFICATIONS
            try
            {
                iOSNotificationCenter.RemoveScheduledNotification(notificationId);
                Debug.Log($"[{Tag}] CancelLocalNotification: {notificationId}");
                return GameResult.Ok();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return GameResult.Failure(GAME_ERROR_TYPE.PUSH_NOTIFICATION_CANCEL_FAILED, ex.Message);
            }
#else
            return GameResult.Failure(GAME_ERROR_TYPE.PUSH_UNSUPPORTED_PLATFORM,
                "Apple push provider is not available on this platform.");
#endif
        }

        public async Task<GameResult> CancelAllLocalNotificationsAsync(CancellationToken ct)
        {
#if UNITY_IOS && !UNITY_EDITOR && UNITY_MOBILE_NOTIFICATIONS
            try
            {
                iOSNotificationCenter.RemoveAllScheduledNotifications();
                Debug.Log($"[{Tag}] CancelAllLocalNotifications");
                return GameResult.Ok();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return GameResult.Failure(GAME_ERROR_TYPE.PUSH_NOTIFICATION_CANCEL_FAILED, ex.Message);
            }
#else
            return GameResult.Failure(GAME_ERROR_TYPE.PUSH_UNSUPPORTED_PLATFORM,
                "Apple push provider is not available on this platform.");
#endif
        }
    }
}
