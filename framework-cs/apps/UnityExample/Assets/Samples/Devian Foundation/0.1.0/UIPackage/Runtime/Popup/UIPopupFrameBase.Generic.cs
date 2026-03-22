using UnityEngine;

namespace Devian
{
    public abstract class UIPopupFrameBase<TReq> : UIPopupFrameBase
    {
        protected TReq CurrentRequest { get; private set; }

        protected sealed override void onBind(object payload)
        {
            CurrentRequest = ConvertRequest(payload);
            onBind(CurrentRequest);
        }

        protected abstract void onBind(TReq request);

        private static TReq ConvertRequest(object payload)
        {
            if (payload == null)
            {
                return default;
            }

            if (payload is TReq typedRequest)
            {
                return typedRequest;
            }

            Debug.LogWarning(
                $"[UIPopupFrameBase<{typeof(TReq).Name}>] " +
                $"Invalid request payload type '{payload.GetType().Name}'. Expected '{typeof(TReq).Name}'.");
            return default;
        }
    }
}
