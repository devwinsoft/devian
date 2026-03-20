using System;

namespace Devian
{
    [Serializable]
    public sealed class UI_TRANSITION_PRESET_ID
    {
        public string Value;

        public bool IsValid => !string.IsNullOrEmpty(Value);

        public static implicit operator string(UI_TRANSITION_PRESET_ID obj)
        {
            return obj == null ? string.Empty : (obj.Value ?? string.Empty);
        }

        public static implicit operator UI_TRANSITION_PRESET_ID(string value)
        {
            return new UI_TRANSITION_PRESET_ID
            {
                Value = value
            };
        }
    }
}
