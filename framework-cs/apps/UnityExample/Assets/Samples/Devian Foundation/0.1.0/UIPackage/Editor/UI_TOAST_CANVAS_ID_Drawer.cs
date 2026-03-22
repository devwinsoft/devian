// SSOT: skills/devian-unity/23-ui-package/52-ui-toast-system/16-ui-toast-canvas-id/SKILL.md

#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace Devian
{
    [CustomPropertyDrawer(typeof(UI_TOAST_CANVAS_ID))]
    public sealed class UI_TOAST_CANVAS_ID_Drawer : BaseEditorID_Drawer<UIToastCanvasIdSelector>
    {
        protected override UIToastCanvasIdSelector GetSelector()
        {
            var w = ScriptableObject.CreateInstance<UIToastCanvasIdSelector>();
            w.ShowUtility();
            return w;
        }
    }
}

#endif
