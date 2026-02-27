#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace Devian
{
    // ============================================================
    // MATERIAL_EFFECT_ID Selector
    // ============================================================

    public sealed class MaterialEffectIdSelector : BaseEditorScriptableAssetIdSelector<MaterialEffectAsset>
    {
        protected override string GroupKey => "MATERIAL_EFFECT";
        protected override string DisplayTypeName => "MATERIAL_EFFECT_ID";
    }

    // ============================================================
    // MATERIAL_EFFECT_ID Drawer
    // ============================================================

    [CustomPropertyDrawer(typeof(MATERIAL_EFFECT_ID))]
    public sealed class MATERIAL_EFFECT_ID_Drawer : BaseEditorID_Drawer<MaterialEffectIdSelector>
    {
        protected override MaterialEffectIdSelector GetSelector()
        {
            var w = ScriptableObject.CreateInstance<MaterialEffectIdSelector>();
            w.titleContent = new GUIContent("Select MATERIAL_EFFECT_ID");
            w.ShowUtility();
            return w;
        }
    }
}

#endif
