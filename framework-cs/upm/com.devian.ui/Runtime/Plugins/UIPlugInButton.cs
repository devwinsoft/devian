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
    public class UIPlugInButton : MonoBehaviour
    {
        public enum EffectType
        {
            Scale,
            AnchoredPosition
        }

        [SerializeField] private EffectType _effectType = EffectType.Scale;
        [SerializeField] private bool useScaling = true;
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
        private Vector3 _originalScale;
        private Vector2 _originalAnchoredPosition;

        private void Awake()
        {
            _trigger = GetComponent<EventTrigger>();
            _rectTransform = GetComponent<RectTransform>();

            if (_rectTransform != null)
            {
                _originalScale = _rectTransform.localScale;
                _originalAnchoredPosition = _rectTransform.anchoredPosition;
            }

            setupTriggers();
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
        }

        private void onPointerDown(BaseEventData eventData)
        {
            if (_rectTransform == null) return;

            if (useScaling)
            {
                _rectTransform.localScale = _originalScale * 1.1f;
            }
            else
            {
                switch (_effectType)
                {
                    case EffectType.Scale:
                        _rectTransform.localScale = _originalScale * 1.1f;
                        break;
                    case EffectType.AnchoredPosition:
                        _rectTransform.anchoredPosition = _originalAnchoredPosition + new Vector2(0, -10f);
                        break;
                }
            }

            // UI Sound (down)
            if (SoundDown != null && SoundDown.IsValid())
            {
                var row = TB_SOUND.Get(SoundDown.Value);
                if (row != null && !string.IsNullOrEmpty(row.Sound_id))
                {
                    SoundManager.Instance.PlaySound(row.Sound_id, channelOverride: SoundChannelType.Ui);
                }
            }

            onDown?.Invoke();
        }

        private void onPointerUp(BaseEventData eventData)
        {
            if (_rectTransform == null) return;

            if (useScaling)
            {
                _rectTransform.localScale = _originalScale;
            }
            else
            {
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

            // UI Sound (up)
            if (SoundUp != null && SoundUp.IsValid())
            {
                var row = TB_SOUND.Get(SoundUp.Value);
                if (row != null && !string.IsNullOrEmpty(row.Sound_id))
                {
                    SoundManager.Instance.PlaySound(row.Sound_id, channelOverride: SoundChannelType.Ui);
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
            if (scroll == null) return;

            // BeginDrag
            var beginDrag = new EventTrigger.Entry();
            beginDrag.eventID = EventTriggerType.BeginDrag;
            beginDrag.callback.AddListener(evt =>
            {
                var pointerData = evt as PointerEventData;
                if (pointerData != null)
                {
                    scroll.OnBeginDrag(pointerData);
                }
            });
            _trigger.triggers.Add(beginDrag);

            // Drag
            var drag = new EventTrigger.Entry();
            drag.eventID = EventTriggerType.Drag;
            drag.callback.AddListener(evt =>
            {
                var pointerData = evt as PointerEventData;
                if (pointerData != null)
                {
                    scroll.OnDrag(pointerData);
                }
            });
            _trigger.triggers.Add(drag);

            // EndDrag
            var endDrag = new EventTrigger.Entry();
            endDrag.eventID = EventTriggerType.EndDrag;
            endDrag.callback.AddListener(evt =>
            {
                var pointerData = evt as PointerEventData;
                if (pointerData != null)
                {
                    scroll.OnEndDrag(pointerData);
                }
            });
            _trigger.triggers.Add(endDrag);

            // PointerDown triggers BeginDrag for immediate scroll response
            var pointerDownDrag = new EventTrigger.Entry();
            pointerDownDrag.eventID = EventTriggerType.PointerDown;
            pointerDownDrag.callback.AddListener(evt =>
            {
                var pointerData = evt as PointerEventData;
                if (pointerData != null)
                {
                    scroll.OnBeginDrag(pointerData);
                }
            });
            _trigger.triggers.Add(pointerDownDrag);
        }
    }
}
