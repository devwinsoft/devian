using System;
using System.Collections.Generic;

namespace Devian
{
    [Serializable]
    public sealed class ScheduledNotificationEntry
    {
        public string notificationId = string.Empty;
        public string title = string.Empty;
        public string body = string.Empty;
        public string fireAt = string.Empty;
        public string repeatInterval = "none";
        public string payload = string.Empty;
    }

    /// <summary>
    /// Push 시스템 로컬/클라우드 저장 구조.
    /// PushManager가 소유한다.
    /// </summary>
    [Serializable]
    public sealed class PushStorage
    {
        public int schemaVersion = 1;

        /// <summary>
        /// 마지막 FCM 토큰 (로컬 캐시, 디버깅/진단 용도).
        /// </summary>
        public string token = string.Empty;

        /// <summary>
        /// 토큰 갱신 시각 (ISO 8601).
        /// </summary>
        public string tokenUpdatedAt = string.Empty;

        /// <summary>
        /// 구독 중인 토픽 ID 목록.
        /// </summary>
        public List<string> subscribedTopics = new();

        /// <summary>
        /// 등록된 로컬 알림 목록.
        /// </summary>
        public List<ScheduledNotificationEntry> scheduledNotifications = new();

        public bool HasScheduledNotification(string notificationId)
        {
            if (string.IsNullOrEmpty(notificationId))
                return false;

            for (var i = 0; i < scheduledNotifications.Count; i++)
            {
                if (string.Equals(scheduledNotifications[i].notificationId, notificationId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        public bool RemoveScheduledNotification(string notificationId)
        {
            if (string.IsNullOrEmpty(notificationId))
                return false;

            for (var i = scheduledNotifications.Count - 1; i >= 0; i--)
            {
                if (string.Equals(scheduledNotifications[i].notificationId, notificationId, StringComparison.Ordinal))
                {
                    scheduledNotifications.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        public void Clear()
        {
            schemaVersion = 1;
            token = string.Empty;
            tokenUpdatedAt = string.Empty;
            subscribedTopics.Clear();
            scheduledNotifications.Clear();
        }
    }
}
