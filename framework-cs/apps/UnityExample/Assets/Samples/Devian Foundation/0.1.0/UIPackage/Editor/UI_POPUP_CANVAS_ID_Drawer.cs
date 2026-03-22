// SSOT: skills/devian-unity/23-ui-package/53-ui-popup-system/17-ui-popup-canvas-id/SKILL.md

#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace Devian
{
    [CustomPropertyDrawer(typeof(UI_POPUP_CANVAS_ID))]
    public sealed class UI_POPUP_CANVAS_ID_Drawer : BaseEditorID_Drawer<UIPopupCanvasIdSelector>
    {
        protected override UIPopupCanvasIdSelector GetSelector()
        {
            var w = ScriptableObject.CreateInstance<UIPopupCanvasIdSelector>();
            w.ShowUtility();
            return w;
        }
    }
}

#endif
