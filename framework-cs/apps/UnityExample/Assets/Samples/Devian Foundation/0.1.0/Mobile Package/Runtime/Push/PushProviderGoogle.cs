using System;
using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Common;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR && UNITY_MOBILE_NOTIFICATIONS
using Unity.Notifications.Android;
#endif

namespace Devian
{
    /// <summary>
    /// Android(FCM) Push Provider.
    /// Firebase Messaging Android SDK를 통해 토큰 획득/토픽 구독을 처리한다.
    /// 로컬 알림은 Unity Mobile Notifications 패키지를 사용한다.
    /// </summary>
    internal sealed class PushProviderGoogle : IPushPlatformProvider
    {
        private const string Tag = nameof(PushProviderGoogle);
        private bool _channelCreated;

        /// <summary>기본 NotificationChannel ID.</summary>
        private const string DefaultChannelId = "devian_push_default";

        /// <summary>
        /// 모든 로컬 알림에 사용하는 기본 SmallIcon ID.
        /// Unity Mobile Notifications 패키지의 NotificationsSettings.asset에
        /// 동일한 ID로 drawable 리소스가 등록되어 있어야 한다.
        /// </summary>
        private const string DefaultSmallIcon = "icon_0";

        /// <summary>
        /// 모든 로컬 알림에 사용하는 기본 LargeIcon ID.
        /// NotificationsSettings.asset에 동일한 ID로 drawable 리소스가 등록되어 있어야 한다.
        /// </summary>
        private const string DefaultLargeIcon = "icon_1";

        public async Task<CommonResult> RequestPermissionAsync(CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                // Android 13+(API 33): POST_NOTIFICATIONS 런타임 권한 요청
                // Android 12 이하: 즉시 granted
#if UNITY_2023_1_OR_NEWER
                if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission("android.permission.POST_NOTIFICATIONS"))
                {
                    UnityEngine.Android.Permission.RequestUserPermission("android.permission.POST_NOTIFICATIONS");
                }
#endif
                ensureNotificationChannel();
                return CommonResult.Ok();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return CommonResult.Failure(COMMON_ERROR_TYPE.PUSH_PERMISSION_DENIED, ex.Message);
            }
#else
            return CommonResult.Failure(COMMON_ERROR_TYPE.PUSH_UNSUPPORTED_PLATFORM,
                "Google push provider is not available on this platform.");
#endif
        }

        public async Task<CommonResult<string>> GetTokenAsync(CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                var token = await Firebase.Messaging.FirebaseMessaging.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return CommonResult<string>.Failure(
                        COMMON_ERROR_TYPE.PUSH_TOKEN_FAILED, "FCM token is empty.");
                }

                return CommonResult<string>.Success(token);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return CommonResult<string>.Failure(COMMON_ERROR_TYPE.PUSH_TOKEN_FAILED, ex.Message);
            }
#else
            return CommonResult<string>.Failure(COMMON_ERROR_TYPE.PUSH_UNSUPPORTED_PLATFORM,
                "Google push provider is not available on this platform.");
#endif
        }

        public async Task<CommonResult> SubscribeTopicAsync(string topicId, CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                await Firebase.Messaging.FirebaseMessaging.SubscribeAsync(topicId);
                return CommonResult.Ok();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return CommonResult.Failure(COMMON_ERROR_TYPE.PUSH_TOPIC_SUBSCRIBE_FAILED, ex.Message);
            }
#else
            return CommonResult.Failure(COMMON_ERROR_TYPE.PUSH_UNSUPPORTED_PLATFORM,
                "Google push provider is not available on this platform.");
#endif
        }

        public async Task<CommonResult> UnsubscribeTopicAsync(string topicId, CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                await Firebase.Messaging.FirebaseMessaging.UnsubscribeAsync(topicId);
                return CommonResult.Ok();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return CommonResult.Failure(COMMON_ERROR_TYPE.PUSH_TOPIC_UNSUBSCRIBE_FAILED, ex.Message);
            }
#else
            return CommonResult.Failure(COMMON_ERROR_TYPE.PUSH_UNSUPPORTED_PLATFORM,
                "Google push provider is not available on this platform.");
#endif
        }

        public async Task<CommonResult> ScheduleLocalNotificationAsync(
            LocalNotificationData data, CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR && UNITY_MOBILE_NOTIFICATIONS
            try
            {
                ensureNotificationChannel();

                var notification = new AndroidNotification
                {
                    Title = data.Title,
                    Text = data.Body,
                    FireTime = data.FireAt,
                    SmallIcon = DefaultSmallIcon,
                    LargeIcon = DefaultLargeIcon,
                    IntentData = data.Payload,
                };

                if (data.Repeat == RepeatInterval.Daily)
                    notification.RepeatInterval = TimeSpan.FromDays(1);
                else if (data.Repeat == RepeatInterval.Weekly)
                    notification.RepeatInterval = TimeSpan.FromDays(7);

                AndroidNotificationCenter.SendNotification(notification, DefaultChannelId);
                Debug.Log($"[{Tag}] ScheduleLocalNotification: {data.NotificationId}");
                return CommonResult.Ok();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return CommonResult.Failure(COMMON_ERROR_TYPE.PUSH_NOTIFICATION_SCHEDULE_FAILED, ex.Message);
            }
#else
            return CommonResult.Failure(COMMON_ERROR_TYPE.PUSH_UNSUPPORTED_PLATFORM,
                "Google push provider is not available on this platform.");
#endif
        }

        public async Task<CommonResult> CancelLocalNotificationAsync(
            string notificationId, CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                // Unity Mobile Notifications는 int ID 기반이지만,
                // PushStorage가 notificationId(string)↔int 매핑을 관리한다.
                // 여기서는 전체 취소(CancelAll)만 사용하는 정책이므로 개별 취소는 로그만 남긴다.
                Debug.Log($"[{Tag}] CancelLocalNotification: {notificationId}");
                return CommonResult.Ok();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return CommonResult.Failure(COMMON_ERROR_TYPE.PUSH_NOTIFICATION_CANCEL_FAILED, ex.Message);
            }
#else
            return CommonResult.Failure(COMMON_ERROR_TYPE.PUSH_UNSUPPORTED_PLATFORM,
                "Google push provider is not available on this platform.");
#endif
        }

        public async Task<CommonResult> CancelAllLocalNotificationsAsync(CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR && UNITY_MOBILE_NOTIFICATIONS
            try
            {
                AndroidNotificationCenter.CancelAllNotifications();
                Debug.Log($"[{Tag}] CancelAllLocalNotifications");
                return CommonResult.Ok();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return CommonResult.Failure(COMMON_ERROR_TYPE.PUSH_NOTIFICATION_CANCEL_FAILED, ex.Message);
            }
#else
            return CommonResult.Failure(COMMON_ERROR_TYPE.PUSH_UNSUPPORTED_PLATFORM,
                "Google push provider is not available on this platform.");
#endif
        }

        // ── Internal ─────────────────────────────────────────

        void ensureNotificationChannel()
        {
#if UNITY_ANDROID && !UNITY_EDITOR && UNITY_MOBILE_NOTIFICATIONS
            if (_channelCreated)
                return;

            _channelCreated = true;

            var channel = new AndroidNotificationChannel
            {
                Id = DefaultChannelId,
                Name = "Push Notifications",
                Importance = Importance.High,
                Description = "Default notification channel for push and local notifications.",
            };
            AndroidNotificationCenter.RegisterNotificationChannel(channel);
            Debug.Log($"[{Tag}] NotificationChannel created: {DefaultChannelId}");
#endif
        }
    }
}
