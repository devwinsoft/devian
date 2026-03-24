using System;

namespace Devian
{
    [Serializable]
    public enum PopupDuplicatePolicy
    {
        Allow,
        IgnoreIfShow,
        FocusIfShow,
        ReplaceIfShow
    }

    [Serializable]
    public enum PopupCloseReason
    {
        Completed,
        Canceled,
        Back,
        Escape,
        DimClick,
        Replaced,
        ForceClosed
    }

    [Serializable]
    public enum PopupFrameState
    {
        Showing,
        Show,
        Closing
    }
}
