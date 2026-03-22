using System;

namespace Devian
{
    [Serializable]
    public enum PopupDuplicatePolicy
    {
        Allow,
        IgnoreIfOpened,
        FocusIfOpened,
        ReplaceIfOpened
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
        Opening,
        Opened,
        Closing
    }
}
