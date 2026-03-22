#if UNITY_EDITOR

namespace Devian
{
    public sealed class UITransitionPresetIdSelector : BaseEditorUIScriptableAssetIdSelector<UITransitionPresetAsset>
    {
        protected override string GroupKey => "UI_TRANSITION_PRESET_ID";
        protected override string DisplayTypeName => "UI_TRANSITION_PRESET_ID";
    }
}

#endif
