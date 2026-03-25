using System;

namespace Devian
{
    /// <summary>
    /// UILoadingCanvas prefab을 참조하기 위한 string wrapper ID.
    /// </summary>
    [Serializable]
    public sealed class UI_LOADING_CANVAS_ID
    {
        public string Value;

        public bool IsValid => !string.IsNullOrEmpty(Value);

        public static implicit operator string(UI_LOADING_CANVAS_ID obj)
        {
            return obj == null ? string.Empty : (obj.Value ?? string.Empty);
        }

        public static implicit operator UI_LOADING_CANVAS_ID(string value)
        {
            return new UI_LOADING_CANVAS_ID
            {
                Value = value
            };
        }
    }
}
