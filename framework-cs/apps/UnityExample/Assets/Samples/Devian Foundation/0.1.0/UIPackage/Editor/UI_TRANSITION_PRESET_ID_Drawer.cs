#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace Devian
{
    [CustomPropertyDrawer(typeof(UI_TRANSITION_PRESET_ID))]
    public sealed class UI_TRANSITION_PRESET_ID_Drawer : BaseEditorID_Drawer<UITransitionPresetIdSelector>
    {
        protected override UITransitionPresetIdSelector GetSelector()
        {
            var window = ScriptableObject.CreateInstance<UITransitionPresetIdSelector>();
            window.ShowUtility();
            return window;
        }
    }
}

#endif
