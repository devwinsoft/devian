namespace Devian
{
    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error
    }

    public enum ToastDuplicatePolicy
    {
        Allow,
        IgnoreIfVisible,
        RefreshDurationIfVisible
    }

    public enum ToastAnchorPreset
    {
        TopLeft,
        TopCenter,
        TopRight,
        MiddleLeft,
        MiddleCenter,
        MiddleRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }
}
