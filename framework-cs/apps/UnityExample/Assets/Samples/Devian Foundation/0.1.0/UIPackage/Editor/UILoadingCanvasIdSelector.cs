// SSOT: skills/devian-unity/23-ui-package/11-ui-loading-system/10-loading-canvas/SKILL.md

#if UNITY_EDITOR

namespace Devian
{
    public sealed class UILoadingCanvasIdSelector : BaseEditorUIAssetIdSelector<UILoadingCanvas>
    {
        protected override string GroupKey => "UI_LOADING_CANVAS_ID";
        protected override string DisplayTypeName => "UI_LOADING_CANVAS_ID";
    }
}

#endif
