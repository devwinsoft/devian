// SSOT: skills/devian-unity/23-ui-package/52-ui-toast-system/16-ui-toast-canvas-id/SKILL.md

#if UNITY_EDITOR

namespace Devian
{
    public sealed class UIToastCanvasIdSelector : BaseEditorUIAssetIdSelector<UIToastCanvas>
    {
        protected override string GroupKey => "UI_TOAST_CANVAS_ID";
        protected override string DisplayTypeName => "UI_TOAST_CANVAS_ID";
    }
}

#endif
