using System;

public sealed class ConfirmPopupRequest
{
    public ConfirmPopupRequest(
        string title,
        string message,
        string confirmText = "Confirm",
        string cancelText = "Cancel")
    {
        Title = title ?? string.Empty;
        Message = message ?? string.Empty;
        ConfirmText = string.IsNullOrWhiteSpace(confirmText) ? "Confirm" : confirmText;
        CancelText = string.IsNullOrWhiteSpace(cancelText) ? "Cancel" : cancelText;
    }

    public string Title { get; }
    public string Message { get; }
    public string ConfirmText { get; }
    public string CancelText { get; }
}
