using Devian;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class UIPopupFrameConfirm : UIPopupFrameBase<ConfirmPopupRequest>
{
    private const string DefaultTitle = "Confirm";
    private const string DefaultMessage = "Continue?";
    private const string TitlePath = "Title";
    private const string MessagePath = "Message";
    private const string ConfirmButtonPath = "ConfirmButton";
    private const string CancelButtonPath = "CancelButton";
    private const string LabelChildName = "Text";

    private TextMeshProUGUI _titleText;
    private TextMeshProUGUI _messageText;
    private Button _confirmButton;
    private Button _cancelButton;
    private TextMeshProUGUI _confirmButtonText;
    private TextMeshProUGUI _cancelButtonText;

    protected override PopupDuplicatePolicy DuplicatePolicy => PopupDuplicatePolicy.FocusIfShow;

    protected override void onAwake()
    {
        base.onAwake();
        ResolveComponents();
    }

    protected override void onInit()
    {
        base.onInit();
        ResolveComponents();
        BindButtonHandlers();
    }

    protected override void onBind(ConfirmPopupRequest request)
    {
        ResolveComponents();

        var boundRequest = request ?? new ConfirmPopupRequest(DefaultTitle, DefaultMessage);

        if (_titleText != null)
        {
            _titleText.text = string.IsNullOrWhiteSpace(boundRequest.Title)
                ? DefaultTitle
                : boundRequest.Title;
        }

        if (_messageText != null)
        {
            _messageText.text = string.IsNullOrWhiteSpace(boundRequest.Message)
                ? DefaultMessage
                : boundRequest.Message;
        }

        if (_confirmButtonText != null)
        {
            _confirmButtonText.text = boundRequest.ConfirmText;
        }

        if (_cancelButtonText != null)
        {
            _cancelButtonText.text = boundRequest.CancelText;
        }
    }

    private void BindButtonHandlers()
    {
        if (_confirmButton != null)
        {
            _confirmButton.onClick.RemoveListener(HandleConfirmClicked);
            _confirmButton.onClick.AddListener(HandleConfirmClicked);
        }

        if (_cancelButton != null)
        {
            _cancelButton.onClick.RemoveListener(HandleCancelClicked);
            _cancelButton.onClick.AddListener(HandleCancelClicked);
        }
    }

    private void ResolveComponents()
    {
        if (_titleText == null)
        {
            _titleText = transform.Find(TitlePath)?.GetComponent<TextMeshProUGUI>();
        }

        if (_messageText == null)
        {
            _messageText = transform.Find(MessagePath)?.GetComponent<TextMeshProUGUI>();
        }

        if (_confirmButton == null)
        {
            _confirmButton = transform.Find(ConfirmButtonPath)?.GetComponent<Button>();
        }

        if (_cancelButton == null)
        {
            _cancelButton = transform.Find(CancelButtonPath)?.GetComponent<Button>();
        }

        if (_confirmButtonText == null)
        {
            _confirmButtonText = transform.Find($"{ConfirmButtonPath}/{LabelChildName}")?.GetComponent<TextMeshProUGUI>();
        }

        if (_cancelButtonText == null)
        {
            _cancelButtonText = transform.Find($"{CancelButtonPath}/{LabelChildName}")?.GetComponent<TextMeshProUGUI>();
        }
    }

    private void HandleConfirmClicked()
    {
        ClosePopup(PopupCloseReason.Completed);
    }

    private void HandleCancelClicked()
    {
        ClosePopup(PopupCloseReason.Canceled);
    }
}
