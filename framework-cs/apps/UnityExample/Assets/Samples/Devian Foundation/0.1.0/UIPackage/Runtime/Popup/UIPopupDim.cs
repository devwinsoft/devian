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

        public RectTransform rectTransform => _rectTransform != null ? _rectTransform : transform as RectTransform;

        private void Awake()
        {
            ResolveDefaults();
        }

        public void ApplyState(bool showDim, bool blockBehind, bool closeOnDimClick, Color dimColor, float dimAlpha)
        {
            ResolveDefaults();

            var alpha = showDim ? Mathf.Clamp01(dimAlpha) : 0f;
            var color = dimColor;
            color.a = alpha;

            _closeOnDimClick = blockBehind && closeOnDimClick;

            _image.color = color;
            _image.raycastTarget = blockBehind;

            _canvasGroup.alpha = alpha;
            _canvasGroup.blocksRaycasts = blockBehind;
            _canvasGroup.interactable = _closeOnDimClick;

            gameObject.SetActive(showDim || blockBehind);
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
