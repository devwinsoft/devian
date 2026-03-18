using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Common;
using Devian.Domain.Operation;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Push 시스템 단일 진입점.
    /// 토큰 획득, 토픽 구독/해제, 로컬 알림 스케줄링을 담당한다.
    /// 서버 의존 없음 — 모든 원격 푸시는 토픽 기반 발송만 사용한다.
    /// </summary>
    public sealed class PushManager : CompoSingleton<PushManager>
    {
        private const string Tag = nameof(PushManager);

        private readonly PushStorage _storage = new();
        private readonly SemaphoreSlim _initializeGate = new SemaphoreSlim(1, 1);

        private IPushPlatformProvider _provider;
        private bool _initialized;
        private bool _permissionGranted;

        // ── Public Properties ────────────────────────────────

        public PushStorage Storage => _storage;
        public bool IsInitialized => _initialized;
        public bool IsPermissionGranted => _permissionGranted;

        // ── Events ───────────────────────────────────────────

        public event Action<bool> OnPermissionResult;

        // ── Lifecycle ────────────────────────────────────────

        protected override void onInitAwake()
        {
            _provider = createProvider();
        }

        protected override void onDestroy()
        {
        }

        // ── Public API ───────────────────────────────────────

        /// <summary>
        /// 권한 요청 → 토큰 획득 → 테이블 기반 토픽 구독 → 저장 토픽 재구독.
        /// Idempotent. Editor에서는 즉시 PUSH_UNSUPPORTED_PLATFORM 반환.
        /// </summary>
        public async Task<CommonResult> InitializeAsync(CancellationToken ct = default)
        {
            await _initializeGate.WaitAsync(ct);
            try
            {
                if (_initialized)
                    return CommonResult.Ok();

                // 1. 권한 요청
                var permResult = await _provider.RequestPermissionAsync(ct);
                _permissionGranted = permResult.IsSuccess;

                try { OnPermissionResult?.Invoke(_permissionGranted); }
                catch (Exception ex) { Debug.LogException(ex); }

                if (permResult.IsFailure)
                {
                    // Unsupported 플랫폼이면 여기서 끝
                    if (isUnsupportedPlatformError(permResult))
                    {
                        Debug.LogWarning($"[{Tag}] Platform not supported: {permResult.Error}");
                        _initialized = true;
                        return permResult;
                    }

                    Debug.LogWarning($"[{Tag}] Permission denied: {permResult.Error}");
                    _initialized = true;
                    return permResult;
                }

                // 2. 토큰 획득 (로컬 캐시만)
                var tokenResult = await _provider.GetTokenAsync(ct);
                if (tokenResult.IsSuccess)
                {
                    _storage.token = tokenResult.Value;
                    _storage.tokenUpdatedAt = DateTime.UtcNow.ToString("O");
                    Debug.Log($"[{Tag}] FCM token acquired.");
                }
                else
                {
                    Debug.LogWarning($"[{Tag}] Token acquisition failed: {tokenResult.Error}");
                }

                // 3. 테이블 기반 토픽 구독
                await subscribeTableTopicsAsync(ct);

                // 4. 저장 토픽 재구독
                await resubscribeStoredTopicsAsync(ct);

                _initialized = true;
                return CommonResult.Ok();
            }
            finally
            {
                _initializeGate.Release();
            }
        }

        /// <summary>
        /// 토픽 구독 + PushStorage.subscribedTopics 동기화.
        /// </summary>
        public async Task<CommonResult> SubscribeTopicAsync(string topicId, CancellationToken ct = default)
        {
            var guard = ensureInitialized();
            if (guard.IsFailure)
                return guard;

            if (string.IsNullOrEmpty(topicId))
                return CommonResult.Failure(COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT, "topicId is empty.");

            var result = await _provider.SubscribeTopicAsync(topicId, ct);
            if (result.IsSuccess)
            {
                if (!_storage.subscribedTopics.Contains(topicId))
                    _storage.subscribedTopics.Add(topicId);

                Debug.Log($"[{Tag}] Subscribed to topic: {topicId}");
            }

            return result;
        }

        /// <summary>
        /// 토픽 해제 + PushStorage.subscribedTopics 동기화.
        /// </summary>
        public async Task<CommonResult> UnsubscribeTopicAsync(string topicId, CancellationToken ct = default)
        {
            var guard = ensureInitialized();
            if (guard.IsFailure)
                return guard;

            if (string.IsNullOrEmpty(topicId))
                return CommonResult.Failure(COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT, "topicId is empty.");

            var result = await _provider.UnsubscribeTopicAsync(topicId, ct);
            if (result.IsSuccess)
            {
                _storage.subscribedTopics.Remove(topicId);
                Debug.Log($"[{Tag}] Unsubscribed from topic: {topicId}");
            }

            return result;
        }

        /// <summary>
        /// 로컬 알림 등록 + PushStorage.scheduledNotifications 동기화.
        /// </summary>
        public async Task<CommonResult> ScheduleLocalNotificationAsync(
            LocalNotificationData data, CancellationToken ct = default)
        {
            var guard = ensureInitialized();
            if (guard.IsFailure)
                return guard;

            if (data == null || string.IsNullOrEmpty(data.NotificationId))
                return CommonResult.Failure(COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT, "notification data or id is empty.");

            if (_storage.HasScheduledNotification(data.NotificationId))
                return CommonResult.Failure(COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT,
                    $"Duplicate notificationId: {data.NotificationId}");

            var result = await _provider.ScheduleLocalNotificationAsync(data, ct);
            if (result.IsSuccess)
            {
                _storage.scheduledNotifications.Add(new ScheduledNotificationEntry
                {
                    notificationId = data.NotificationId,
                    title = data.Title ?? string.Empty,
                    body = data.Body ?? string.Empty,
                    fireAt = data.FireAt.ToString("O"),
                    repeatInterval = data.Repeat.ToString().ToLowerInvariant(),
                    payload = data.Payload ?? string.Empty,
                });

                Debug.Log($"[{Tag}] Scheduled local notification: {data.NotificationId}");
            }

            return result;
        }

        /// <summary>
        /// 로컬 알림 취소 + PushStorage.scheduledNotifications에서 제거.
        /// </summary>
        public async Task<CommonResult> CancelLocalNotificationAsync(
            string notificationId, CancellationToken ct = default)
        {
            var guard = ensureInitialized();
            if (guard.IsFailure)
                return guard;

            if (string.IsNullOrEmpty(notificationId))
                return CommonResult.Failure(COMMON_ERROR_TYPE.COMMON_INVALID_ARGUMENT, "notificationId is empty.");

            var result = await _provider.CancelLocalNotificationAsync(notificationId, ct);
            if (result.IsSuccess)
            {
                _storage.RemoveScheduledNotification(notificationId);
                Debug.Log($"[{Tag}] Cancelled local notification: {notificationId}");
            }

            return result;
        }

        /// <summary>
        /// 모든 로컬 알림 취소 + PushStorage.scheduledNotifications 초기화.
        /// </summary>
        public async Task<CommonResult> CancelAllLocalNotificationsAsync(CancellationToken ct = default)
        {
            var guard = ensureInitialized();
            if (guard.IsFailure)
                return guard;

            var result = await _provider.CancelAllLocalNotificationsAsync(ct);
            if (result.IsSuccess)
            {
                _storage.scheduledNotifications.Clear();
                Debug.Log($"[{Tag}] Cancelled all local notifications.");
            }

            return result;
        }

        /// <summary>
        /// 저장 데이터 초기화.
        /// </summary>
        public void ClearStorage()
        {
            _storage.Clear();
        }

        // ── Internal ─────────────────────────────────────────

        static IPushPlatformProvider createProvider()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return new PushProviderApple();
#elif UNITY_ANDROID && !UNITY_EDITOR
            return new PushProviderGoogle();
#else
            return new PushProviderUnsupported();
#endif
        }

        CommonResult ensureInitialized()
        {
            if (!_initialized)
                return CommonResult.Failure(COMMON_ERROR_TYPE.PUSH_NOT_INITIALIZED, "PushManager not initialized.");

            return CommonResult.Ok();
        }

        static bool isUnsupportedPlatformError(CommonResult result)
        {
            return result.IsFailure
                   && result.Error != null
                   && result.Error.Code == COMMON_ERROR_TYPE.PUSH_UNSUPPORTED_PLATFORM;
        }

        async Task subscribeTableTopicsAsync(CancellationToken ct)
        {
            if (!TB_PUSH_REMOTE.IsLoaded)
            {
                Debug.LogWarning($"[{Tag}] TB_PUSH_REMOTE not loaded, skipping table-driven topic subscription.");
                return;
            }

            var lang = MobileApplication.Instance.DefaultLanguage.ToString();
            var rows = TB_PUSH_REMOTE.GetByGroup(lang);
            var isDev = Debug.isDebugBuild;

            for (var i = 0; i < rows.Count; i++)
            {
                // Release build: skip test topics
                if (!isDev && rows[i].IsTest)
                    continue;

                var pushId = rows[i].PushId;
                if (string.IsNullOrEmpty(pushId))
                    continue;

                if (_storage.subscribedTopics.Contains(pushId))
                    continue;

                var result = await _provider.SubscribeTopicAsync(pushId, ct);
                if (result.IsSuccess)
                {
                    _storage.subscribedTopics.Add(pushId);
                    Debug.Log($"[{Tag}] Table topic subscribed: {pushId} (lang={lang})");
                }
                else
                {
                    Debug.LogWarning($"[{Tag}] Failed to subscribe table topic: {pushId} - {result.Error}");
                }
            }
        }

        async Task resubscribeStoredTopicsAsync(CancellationToken ct)
        {
            if (_storage.subscribedTopics.Count <= 0)
                return;

            // 복사 후 순회 (구독 실패 시 원본 리스트 변경 방지)
            var topics = new List<string>(_storage.subscribedTopics);
            var failedTopics = new List<string>();

            for (var i = 0; i < topics.Count; i++)
            {
                var topicId = topics[i];
                if (string.IsNullOrEmpty(topicId))
                    continue;

                var result = await _provider.SubscribeTopicAsync(topicId, ct);
                if (result.IsFailure)
                {
                    Debug.LogWarning($"[{Tag}] Failed to resubscribe topic: {topicId} - {result.Error}");
                    failedTopics.Add(topicId);
                }
            }

            // 재구독 실패한 토픽은 리스트에서 제거하지 않는다 (다음 초기화 시 재시도)
            if (failedTopics.Count > 0)
                Debug.LogWarning($"[{Tag}] {failedTopics.Count} topic(s) failed to resubscribe.");
        }
    }
}
