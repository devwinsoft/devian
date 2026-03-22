#if UNITY_EDITOR

namespace Devian
{
    public sealed class UIPopupFrameIdSelector : BaseEditorUIAssetIdSelector<UIPopupFrameBase>
    {
        protected override string GroupKey => "UI_POPUP_FRAME_ID";
        protected override string DisplayTypeName => "UI_POPUP_FRAME_ID";
    }
}

#endif
