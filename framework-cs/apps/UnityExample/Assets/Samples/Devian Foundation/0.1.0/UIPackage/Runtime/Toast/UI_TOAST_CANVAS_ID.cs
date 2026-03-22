using System;

namespace Devian
{
    /// <summary>
    /// UIToastCanvas prefab을 참조하기 위한 string wrapper ID.
    /// SSOT: skills/devian-unity/23-ui-package/52-ui-toast-system/16-ui-toast-canvas-id/SKILL.md
    /// </summary>
    [Serializable]
    public sealed class UI_TOAST_CANVAS_ID
    {
        public string Value;

        public bool IsValid => !string.IsNullOrEmpty(Value);

        public static implicit operator string(UI_TOAST_CANVAS_ID obj)
        {
            return obj == null ? string.Empty : (obj.Value ?? string.Empty);
        }

        public static implicit operator UI_TOAST_CANVAS_ID(string value)
        {
            return new UI_TOAST_CANVAS_ID
            {
                Value = value
            };
        }
    }
}
