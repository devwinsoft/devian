#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace Devian
{
    // ============================================================
    // EFFECT_ID Selector
    // ============================================================

    public sealed class EffectIdSelector : EditorAssetIdSelectorBase<EffectObject>
    {
        protected override string GroupKey => "EFFECT";
        protected override string DisplayTypeName => "EFFECT_ID";
    }

    // ============================================================
    // EFFECT_ID Drawer
    // ============================================================

    [CustomPropertyDrawer(typeof(EFFECT_ID))]
    public sealed class EFFECT_ID_Drawer : EditorID_DrawerBase<EffectIdSelector>
    {
        protected override EffectIdSelector GetSelector()
        {
            var w = ScriptableObject.CreateInstance<EffectIdSelector>();
            w.titleContent = new GUIContent("Select EFFECT");
            w.ShowUtility();
            return w;
        }
    }
}

#endif
