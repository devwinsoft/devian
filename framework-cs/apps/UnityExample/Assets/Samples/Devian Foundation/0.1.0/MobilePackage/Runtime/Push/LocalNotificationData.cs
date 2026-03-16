using System;

namespace Devian
{
    public enum RepeatInterval
    {
        None = 0,
        Daily = 1,
        Weekly = 2,
    }

    /// <summary>
    /// 로컬 푸시 알림 등록 DTO.
    /// </summary>
    [Serializable]
    public sealed class LocalNotificationData
    {
        public string NotificationId = string.Empty;
        public string Title = string.Empty;
        public string Body = string.Empty;
        public DateTime FireAt;
        public RepeatInterval Repeat = RepeatInterval.None;

        /// <summary>
        /// 커스텀 페이로드 (JSON string). 비어있어도 된다.
        /// </summary>
        public string Payload = string.Empty;
    }
}
