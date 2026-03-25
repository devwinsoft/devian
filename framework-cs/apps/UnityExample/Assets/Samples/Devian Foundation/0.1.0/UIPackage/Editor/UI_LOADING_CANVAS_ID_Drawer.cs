#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace Devian
{
    [CustomPropertyDrawer(typeof(UI_LOADING_CANVAS_ID))]
    public sealed class UI_LOADING_CANVAS_ID_Drawer : BaseEditorID_Drawer<UILoadingCanvasIdSelector>
    {
        protected override UILoadingCanvasIdSelector GetSelector()
        {
            var window = ScriptableObject.CreateInstance<UILoadingCanvasIdSelector>();
            window.ShowUtility();
            return window;
        }
    }
}

#endif
