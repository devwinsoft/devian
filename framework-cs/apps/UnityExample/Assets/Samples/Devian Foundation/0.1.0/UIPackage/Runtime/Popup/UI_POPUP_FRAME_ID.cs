using System;

namespace Devian
{
    [Serializable]
    public sealed class UI_POPUP_FRAME_ID
    {
        public string Value;

        public bool IsValid => !string.IsNullOrEmpty(Value);

        public static implicit operator string(UI_POPUP_FRAME_ID obj)
        {
            return obj == null ? string.Empty : (obj.Value ?? string.Empty);
        }

        public static implicit operator UI_POPUP_FRAME_ID(string value)
        {
            return new UI_POPUP_FRAME_ID
            {
                Value = value
            };
        }
    }
}
