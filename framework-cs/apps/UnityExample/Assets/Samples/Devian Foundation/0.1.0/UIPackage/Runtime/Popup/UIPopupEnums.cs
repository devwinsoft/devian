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
        Confirm,
        Yes,
        No,
        Cancel
    }

    [Serializable]
    public enum PopupFrameState
    {
        Showing,
        Show,
        Closing
    }
}
