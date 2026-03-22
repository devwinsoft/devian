using System;

namespace Devian
{
    public readonly struct PopupRequest
    {
        public readonly string PopupId;
        public readonly object Payload;
        public readonly Action<PopupResult> OnClosed;

        public PopupRequest(string popupId, object payload = null, Action<PopupResult> onClosed = null)
        {
            PopupId = string.IsNullOrWhiteSpace(popupId)
                ? UIPopupDefaults.DefaultPopupId
                : popupId;
            Payload = payload;
            OnClosed = onClosed;
        }
    }
}
