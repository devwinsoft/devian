using Devian;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class UISystenPopupConfirm : UIPopupFrameBase
{
    public TextMeshProUGUI _titleText;
    public TextMeshProUGUI _messageText;
    public Button _confirmButton;
    public Button _cancelButton;

    protected override PopupDuplicatePolicy DuplicatePolicy => PopupDuplicatePolicy.FocusIfShow;

    protected override void onInit()
    {
        base.onInit();
        BindButtonHandlers();
    }

    protected override void onBind(object payload)
    {
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

    private void HandleConfirmClicked()
    {
        ClosePopup(PopupCloseReason.Yes);
    }

    private void HandleCancelClicked()
    {
        ClosePopup(PopupCloseReason.No);
    }
}
