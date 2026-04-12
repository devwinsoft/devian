using Devian.Domain.Sound;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Devian
{
    /// <summary>
    /// Button press visual feedback plugin with UnityEvent hooks, optional UI sound playback,
    /// and ScrollRect drag bridge.
    /// </summary>
    public class UIComponentButton : UIComponentBase,
        IPointerDownHandler,
        IPointerUpHandler,
        IInitializePotentialDragHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IScrollHandler
    {
        public enum EffectType
        {
            Scale,
            AnchoredPosition
        }

        [SerializeField] private EffectType _effectType = EffectType.Scale;
        [SerializeField] private SOUND_ID SoundDown;
        [SerializeField] private SOUND_ID SoundUp;

        /// <summary>
        /// Invoked when pointer down occurs (after visual feedback).
        /// </summary>
        public UnityEvent onDown;

        /// <summary>
        /// Invoked when pointer up occurs (after visual feedback).
        /// </summary>
        public UnityEvent onUp;

        /// <summary>
        /// Invoked when pointer is released over the same target without dragging.
        /// </summary>
        public Button.ButtonClickedEvent onClick = new Button.ButtonClickedEvent();

        private RectTransform _rectTransform;
        private Selectable _selectable;
        private ScrollRect _scrollRect;
        private Vector3 _originalScale;
        private Vector2 _originalAnchoredPosition;
        private bool _isDragging;
        private bool _isPointerDown;
        private int _pressedPointerId = int.MinValue;

        protected override void onAwake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _selectable = GetComponent<Selectable>();
            resolveScrollRectIfNeeded();

            if (_rectTransform != null)
            {
                _originalScale = _rectTransform.localScale;
                _originalAnchoredPosition = _rectTransform.anchoredPosition;
            }
        }

        protected override void onPoolDespawned()
        {
            _isPointerDown = false;
            _pressedPointerId = int.MinValue;
            _isDragging = false;
            restoreVisualState();
        }

        protected override void onDestroy()
        {
            _isPointerDown = false;
            _pressedPointerId = int.MinValue;
            _isDragging = false;
            restoreVisualState();
        }

        private void OnDisable()
        {
            _isPointerDown = false;
            _pressedPointerId = int.MinValue;
            _isDragging = false;
            restoreVisualState();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!canHandlePointerEvent(eventData))
                return;

            _isDragging = false;
            _isPointerDown = true;
            _pressedPointerId = eventData.pointerId;
            applyPressedVisualState();

            // UI Sound (down)
            if (SoundDown != null && SoundDown.IsValid())
            {
                var row = TB_SOUND.Get(SoundDown.Value);
                if (row != null && !string.IsNullOrEmpty(row.sound_id))
                {
                    SoundManager.Instance.PlaySound(row.sound_id, channelOverride: SoundChannelType.Ui);
                }
            }

            onDown?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            restoreVisualState();

            if (!_isPointerDown || _pressedPointerId != eventData.pointerId)
                return;

            _isPointerDown = false;
            _pressedPointerId = int.MinValue;

            if (_isDragging)
            {
                _isDragging = false;
                return;
            }

            // UI Sound (up)
            if (SoundUp != null && SoundUp.IsValid())
            {
                var row = TB_SOUND.Get(SoundUp.Value);
                if (row != null && !string.IsNullOrEmpty(row.sound_id))
                {
                    SoundManager.Instance.PlaySound(row.sound_id, channelOverride: SoundChannelType.Ui);
                }
            }

            onUp?.Invoke();

            if (isReleasedOverSelf(eventData))
                onClick?.Invoke();
        }

        /// <summary>
        /// Bridges drag events to a ScrollRect for nested scroll support.
        /// </summary>
        /// <param name="scroll">The ScrollRect to receive drag events.</param>
        public void SetScroll(ScrollRect scroll)
        {
            _scrollRect = scroll;
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (!tryGetScrollPointerEventData(eventData, out var scrollRect))
                return;

            scrollRect.OnInitializePotentialDrag(eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!canHandlePointerEvent(eventData))
                return;

            _isDragging = true;
            restoreVisualState();

            if (!tryGetScrollPointerEventData(eventData, out var scrollRect))
                return;

            scrollRect.OnBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!tryGetScrollPointerEventData(eventData, out var scrollRect))
                return;

            scrollRect.OnDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!tryGetScrollPointerEventData(eventData, out var scrollRect))
                return;

            scrollRect.OnEndDrag(eventData);
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (!tryGetScrollPointerEventData(eventData, out var scrollRect))
                return;

            scrollRect.OnScroll(eventData);
        }

        private bool tryGetScrollPointerEventData(
            PointerEventData eventData,
            out ScrollRect scrollRect)
        {
            scrollRect = resolveScrollRectIfNeeded();
            return eventData != null && scrollRect != null && scrollRect.isActiveAndEnabled;
        }

        private ScrollRect resolveScrollRectIfNeeded()
        {
            if (_scrollRect != null && !transform.IsChildOf(_scrollRect.transform))
                _scrollRect = null;

            if (_scrollRect == null)
            {
                var parent = transform.parent;
                _scrollRect = parent != null
                    ? parent.GetComponentInParent<ScrollRect>()
                    : null;
            }

            return _scrollRect;
        }

        public void AddClickListener(UnityAction listener)
        {
            onClick.AddListener(listener);
        }

        public void RemoveClickListener(UnityAction listener)
        {
            onClick.RemoveListener(listener);
        }

        private void applyPressedVisualState()
        {
            if (_rectTransform == null) return;

            switch (_effectType)
            {
                case EffectType.Scale:
                    _rectTransform.localScale = _originalScale * 0.9f;
                    break;
                case EffectType.AnchoredPosition:
                    _originalAnchoredPosition = _rectTransform.anchoredPosition;
                    _rectTransform.anchoredPosition =
                        _originalAnchoredPosition + new Vector2(0, -10f);
                    break;
            }
        }

        private void restoreVisualState()
        {
            if (_rectTransform == null) return;

            switch (_effectType)
            {
                case EffectType.Scale:
                    _rectTransform.localScale = _originalScale;
                    break;
                case EffectType.AnchoredPosition:
                    _rectTransform.anchoredPosition = _originalAnchoredPosition;
                    break;
            }
        }

        private bool canHandlePointerEvent(PointerEventData eventData)
        {
            if (eventData == null || !isActiveAndEnabled)
                return false;

            if (_selectable != null && !_selectable.IsInteractable())
                return false;

            return eventData.button == PointerEventData.InputButton.Left;
        }

        private bool isReleasedOverSelf(PointerEventData eventData)
        {
            if (_rectTransform == null || eventData == null)
                return false;

            var raycastTarget = eventData.pointerCurrentRaycast.gameObject;
            if (raycastTarget != null)
            {
                if (raycastTarget == gameObject || raycastTarget.transform.IsChildOf(transform))
                    return true;
            }

            return RectTransformUtility.RectangleContainsScreenPoint(
                _rectTransform,
                eventData.position,
                eventData.pressEventCamera);
        }
    }
}
