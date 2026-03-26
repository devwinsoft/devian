using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Devian
{
    [RequireComponent(typeof(Image))]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UIPopupDim : MonoBehaviour, IPointerClickHandler
    {
        private Image _image;
        private CanvasGroup _canvasGroup;
        private RectTransform _rectTransform;
        private bool _closeOnDimClick;

        // prefab에 설정된 원본 색상을 캐시 (스크립트에서 덮어쓰지 않음)
        private Color _originalColor;
        private bool _originalCaptured;

        public RectTransform rectTransform => _rectTransform != null ? _rectTransform : transform as RectTransform;

        private void Awake()
        {
            ResolveDefaults();
            CaptureOriginalColor();
        }

        public void ApplyState(bool showDim, bool blockBehind, bool closeOnDimClick)
        {
            ResolveDefaults();
            CaptureOriginalColor();

            var alpha = showDim ? _originalColor.a : 0f;
            var color = _originalColor;
            color.a = alpha;

            _closeOnDimClick = blockBehind && closeOnDimClick;

            _image.color = color;
            _image.raycastTarget = blockBehind;

            _canvasGroup.alpha = alpha;
            _canvasGroup.blocksRaycasts = blockBehind;
            _canvasGroup.interactable = _closeOnDimClick;

            gameObject.SetActive(showDim || blockBehind);
        }

        private void CaptureOriginalColor()
        {
            if (_originalCaptured) return;
            if (_image != null)
            {
                _originalColor = _image.color;
                _originalCaptured = true;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_closeOnDimClick)
            {
                return;
            }

            if (UIPopupManager.TryGet(out var manager))
            {
                manager.HandleDimClicked();
            }
        }

        private void ResolveDefaults()
        {
            if (_rectTransform == null)
            {
                _rectTransform = transform as RectTransform;
            }

            if (_image == null)
            {
                _image = GetComponent<Image>();
            }

            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
            }
        }
    }
}
