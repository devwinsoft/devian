using System;
using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Common;
using UnityEngine;

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
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                ensureNotificationChannel();

                // TODO: Unity.Notifications.Android 패키지 사용
                // AndroidNotificationCenter.SendNotificationWithExplicitIntent(...)
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
                // TODO: AndroidNotificationCenter.CancelNotification(notificationId)
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
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                // TODO: AndroidNotificationCenter.CancelAllNotifications()
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
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_channelCreated)
                return;

            _channelCreated = true;

            // TODO: Unity.Notifications.Android 패키지 사용
            // var channel = new AndroidNotificationChannel
            // {
            //     Id = "devian_push_default",
            //     Name = "Push Notifications",
            //     Importance = Importance.High,
            // };
            // AndroidNotificationCenter.RegisterNotificationChannel(channel);
            Debug.Log($"[{Tag}] NotificationChannel created: devian_push_default");
#endif
        }
    }
}
