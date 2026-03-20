using System;

namespace Devian
{
    public readonly struct ToastRequest
    {
        public readonly string GroupId;
        public readonly string Message;
        public readonly float? DurationOverride;
        public readonly ToastType ToastType;

        public ToastRequest(
            string groupId,
            string message,
            float? durationOverride = null,
            ToastType toastType = ToastType.Info)
        {
            GroupId = string.IsNullOrEmpty(groupId) ? UIToastDefaults.DefaultGroupId : groupId;
            Message = message ?? string.Empty;
            DurationOverride = durationOverride;
            ToastType = toastType;
        }
    }
}
