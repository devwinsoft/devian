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
    [RequireComponent(typeof(EventTrigger))]
    public class UIComponentButton : UIComponentBase
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

        private EventTrigger _trigger;
        private RectTransform _rectTransform;
        private ScrollRect _scrollRect;
        private Vector3 _originalScale;
        private Vector2 _originalAnchoredPosition;
        private bool _isDragging;

        protected override void onAwake()
        {
            _trigger = GetComponent<EventTrigger>();
            _rectTransform = GetComponent<RectTransform>();
            resolveScrollRectIfNeeded();

            if (_rectTransform != null)
            {
                _originalScale = _rectTransform.localScale;
                _originalAnchoredPosition = _rectTransform.anchoredPosition;
            }

            setupTriggers();
        }

        protected override void onPoolDespawned()
        {
            _isDragging = false;
            restoreVisualState();
        }

        protected override void onDestroy()
        {
            _isDragging = false;
            restoreVisualState();
        }

        private void OnDisable()
        {
            _isDragging = false;
            restoreVisualState();
        }

        private void setupTriggers()
        {
            if (_trigger.triggers == null)
            {
                _trigger.triggers = new System.Collections.Generic.List<EventTrigger.Entry>();
            }

            _trigger.triggers.Clear();

            // PointerDown
            var pointerDown = new EventTrigger.Entry();
            pointerDown.eventID = EventTriggerType.PointerDown;
            pointerDown.callback.AddListener(onPointerDown);
            _trigger.triggers.Add(pointerDown);

            // PointerUp
            var pointerUp = new EventTrigger.Entry();
            pointerUp.eventID = EventTriggerType.PointerUp;
            pointerUp.callback.AddListener(onPointerUp);
            _trigger.triggers.Add(pointerUp);

            // InitializePotentialDrag
            var initializePotentialDrag = new EventTrigger.Entry();
            initializePotentialDrag.eventID = EventTriggerType.InitializePotentialDrag;
            initializePotentialDrag.callback.AddListener(onInitializePotentialDrag);
            _trigger.triggers.Add(initializePotentialDrag);

            // BeginDrag
            var beginDrag = new EventTrigger.Entry();
            beginDrag.eventID = EventTriggerType.BeginDrag;
            beginDrag.callback.AddListener(onBeginDrag);
            _trigger.triggers.Add(beginDrag);

            // Drag
            var drag = new EventTrigger.Entry();
            drag.eventID = EventTriggerType.Drag;
            drag.callback.AddListener(onDrag);
            _trigger.triggers.Add(drag);

            // EndDrag
            var endDrag = new EventTrigger.Entry();
            endDrag.eventID = EventTriggerType.EndDrag;
            endDrag.callback.AddListener(onEndDrag);
            _trigger.triggers.Add(endDrag);

            // Scroll
            var scroll = new EventTrigger.Entry();
            scroll.eventID = EventTriggerType.Scroll;
            scroll.callback.AddListener(onScroll);
            _trigger.triggers.Add(scroll);
        }

        private void onPointerDown(BaseEventData eventData)
        {
            _isDragging = false;
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

        private void onPointerUp(BaseEventData eventData)
        {
            restoreVisualState();
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
        }

        /// <summary>
        /// Bridges drag events to a ScrollRect for nested scroll support.
        /// </summary>
        /// <param name="scroll">The ScrollRect to receive drag events.</param>
        public void SetScroll(ScrollRect scroll)
        {
            _scrollRect = scroll;
        }

        private void onInitializePotentialDrag(BaseEventData eventData)
        {
            if (!tryGetScrollPointerEventData(eventData, out var pointerData, out var scrollRect))
                return;

            scrollRect.OnInitializePotentialDrag(pointerData);
        }

        private void onBeginDrag(BaseEventData eventData)
        {
            if (!(eventData is PointerEventData))
                return;

            _isDragging = true;
            restoreVisualState();

            if (!tryGetScrollPointerEventData(eventData, out var pointerData, out var scrollRect))
                return;

            scrollRect.OnBeginDrag(pointerData);
        }

        private void onDrag(BaseEventData eventData)
        {
            if (!tryGetScrollPointerEventData(eventData, out var pointerData, out var scrollRect))
                return;

            scrollRect.OnDrag(pointerData);
        }

        private void onEndDrag(BaseEventData eventData)
        {
            if (!tryGetScrollPointerEventData(eventData, out var pointerData, out var scrollRect))
                return;

            scrollRect.OnEndDrag(pointerData);
        }

        private void onScroll(BaseEventData eventData)
        {
            if (!tryGetScrollPointerEventData(eventData, out var pointerData, out var scrollRect))
                return;

            scrollRect.OnScroll(pointerData);
        }

        private bool tryGetScrollPointerEventData(
            BaseEventData eventData,
            out PointerEventData pointerData,
            out ScrollRect scrollRect)
        {
            pointerData = eventData as PointerEventData;
            scrollRect = resolveScrollRectIfNeeded();
            return pointerData != null && scrollRect != null && scrollRect.isActiveAndEnabled;
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

        private void applyPressedVisualState()
        {
            if (_rectTransform == null) return;

            switch (_effectType)
            {
                case EffectType.Scale:
                    _rectTransform.localScale = _originalScale * 0.9f;
                    break;
                case EffectType.AnchoredPosition:
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
    }
}
