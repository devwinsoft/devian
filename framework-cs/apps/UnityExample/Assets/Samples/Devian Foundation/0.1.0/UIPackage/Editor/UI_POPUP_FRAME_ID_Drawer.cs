#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace Devian
{
    [CustomPropertyDrawer(typeof(UI_POPUP_FRAME_ID))]
    public sealed class UI_POPUP_FRAME_ID_Drawer : BaseEditorID_Drawer<UIPopupFrameIdSelector>
    {
        protected override UIPopupFrameIdSelector GetSelector()
        {
            var window = ScriptableObject.CreateInstance<UIPopupFrameIdSelector>();
            window.ShowUtility();
            return window;
        }
    }
}

#endif
