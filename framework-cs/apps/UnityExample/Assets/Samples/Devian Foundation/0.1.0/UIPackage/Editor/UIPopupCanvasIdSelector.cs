// SSOT: skills/devian-unity/23-ui-package/53-ui-popup-system/17-ui-popup-canvas-id/SKILL.md

#if UNITY_EDITOR

namespace Devian
{
    public sealed class UIPopupCanvasIdSelector : BaseEditorUIAssetIdSelector<UIPopupCanvas>
    {
        protected override string GroupKey => "UI_POPUP_CANVAS_ID";
        protected override string DisplayTypeName => "UI_POPUP_CANVAS_ID";
    }
}

#endif
