#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace Devian
{
    // ============================================================
    // COMMON_EFFECT_ID Selector
    // ============================================================

    public sealed class CommonEffectIdSelector : BaseEditorAssetIdSelector<CommonEffectObject>
    {
        protected override string GroupKey => "COMMON_EFFECT";
        protected override string DisplayTypeName => "COMMON_EFFECT_ID";
    }

    // ============================================================
    // COMMON_EFFECT_ID Drawer
    // ============================================================

    [CustomPropertyDrawer(typeof(COMMON_EFFECT_ID))]
    public sealed class COMMON_EFFECT_ID_Drawer : BaseEditorID_Drawer<CommonEffectIdSelector>
    {
        protected override CommonEffectIdSelector GetSelector()
        {
            var w = ScriptableObject.CreateInstance<CommonEffectIdSelector>();
            w.titleContent = new GUIContent("Select COMMON_EFFECT");
            w.ShowUtility();
            return w;
        }
    }
}

#endif
