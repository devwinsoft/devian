using System;

namespace Devian
{
    [Serializable]
    public sealed class RENDER_EFFECT_ID
    {
        public string Value;

        public bool IsValid => !string.IsNullOrEmpty(Value);

        public static implicit operator string(RENDER_EFFECT_ID obj)
        {
            return obj == null ? string.Empty : (obj.Value ?? string.Empty);
        }

        public static implicit operator RENDER_EFFECT_ID(string value)
        {
            return new RENDER_EFFECT_ID { Value = value };
        }
    }
}
