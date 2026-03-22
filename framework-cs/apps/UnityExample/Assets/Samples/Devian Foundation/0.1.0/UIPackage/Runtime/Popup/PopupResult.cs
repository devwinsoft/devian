namespace Devian
{
    public readonly struct PopupResult
    {
        public readonly string PopupId;
        public readonly PopupCloseReason Reason;
        public readonly object Payload;

        public PopupResult(string popupId, PopupCloseReason reason, object payload)
        {
            PopupId = popupId;
            Reason = reason;
            Payload = payload;
        }
    }
}
