using System;

namespace Devian
{
    /// <summary>
    /// UIPopupCanvas prefab을 참조하기 위한 string wrapper ID.
    /// SSOT: skills/devian-unity/23-ui-package/53-ui-popup-system/17-ui-popup-canvas-id/SKILL.md
    /// </summary>
    [Serializable]
    public sealed class UI_POPUP_CANVAS_ID
    {
        public string Value;

        public bool IsValid => !string.IsNullOrEmpty(Value);

        public static implicit operator string(UI_POPUP_CANVAS_ID obj)
        {
            return obj == null ? string.Empty : (obj.Value ?? string.Empty);
        }

        public static implicit operator UI_POPUP_CANVAS_ID(string value)
        {
            return new UI_POPUP_CANVAS_ID
            {
                Value = value
            };
        }
    }
}
