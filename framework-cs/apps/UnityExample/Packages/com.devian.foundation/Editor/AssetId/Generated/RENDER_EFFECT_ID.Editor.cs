#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace Devian
{
    // ============================================================
    // RENDER_EFFECT_ID Selector
    // ============================================================

    public sealed class RenderEffectIdSelector : EditorScriptableAssetIdSelectorBase<RenderEffectAsset>
    {
        protected override string GroupKey => "RENDER_EFFECT_GROUP";
        protected override string DisplayTypeName => "RENDER_EFFECT_ID";
    }

    // ============================================================
    // RENDER_EFFECT_ID Drawer
    // ============================================================

    [CustomPropertyDrawer(typeof(RENDER_EFFECT_ID))]
    public sealed class RENDER_EFFECT_ID_Drawer : EditorID_DrawerBase<RenderEffectIdSelector>
    {
        protected override RenderEffectIdSelector GetSelector()
        {
            var w = ScriptableObject.CreateInstance<RenderEffectIdSelector>();
            w.titleContent = new GUIContent("Select RENDER_EFFECT_ID");
            w.ShowUtility();
            return w;
        }
    }
}

#endif
