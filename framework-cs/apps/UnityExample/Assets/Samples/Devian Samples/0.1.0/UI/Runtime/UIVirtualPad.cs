// UIVirtualPad.cs
// UGUI virtual pad (joystick) for mobile.
// - Supports fixed center or dynamic center (appears where you touch).
// - Outputs normalized Vector2 (-1..1) via OnValueChanged and CurrentValue.
// - UGUI only (Image + RectTransform). No InputSystem dependency.
// - Pivot/Anchor/Scale independent: all calculations use local coordinates.

using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Devian
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    [RequireComponent(typeof(UIPlugInNonDrawing))]
    public sealed class UIVirtualPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Serializable]
        public sealed class Vector2Event : UnityEvent<Vector2> { }

        [Header("References (UGUI)")]
        [SerializeField] private RectTransform mOuter;   // large ring (semi-transparent)
        [SerializeField] private RectTransform mInner;   // small knob (opaque)

        [Header("Behavior")]
        [Tooltip("If true, the pad center moves to the touch position on pointer down.")]
        [SerializeField] private bool mDynamicCenter = true;

        [Tooltip("Maximum movement radius of the inner knob in pixels.")]
        [SerializeField] private float mRadius = 120f;

        [Tooltip("Deadzone in normalized units (0..1). Values inside deadzone output 0.")]
        [Range(0f, 0.9f)]
        [SerializeField] private float mDeadzone = 0.1f;

        [Tooltip("If true, hides the pad visuals when not pressed.")]
        [SerializeField] private bool mHideWhenIdle = true;

        [Header("Output")]
        [SerializeField] private Vector2Event mOnValueChanged = new Vector2Event();
        [SerializeField] private UnityEvent mOnPressed = new UnityEvent();
        [SerializeField] private UnityEvent mOnReleased = new UnityEvent();

        public Vector2 CurrentValue { get; private set; } = Vector2.zero;
        public Vector2 Direction => (CurrentValue.sqrMagnitude > 0f) ? CurrentValue.normalized : Vector2.zero;
        // Right(1,0)=0°, Up(0,1)=90°
        public float AngleDeg => Mathf.Atan2(CurrentValue.y, CurrentValue.x) * Mathf.Rad2Deg;
        public bool IsPressed { get; private set; }

        public Vector2Event OnValueChanged => mOnValueChanged;
        public UnityEvent OnPressed => mOnPressed;
        public UnityEvent OnReleased => mOnReleased;

        private Vector2 mLastValueSent = new Vector2(float.NaN, float.NaN);

        private void Reset()
        {
            mRadius = 120f;
            mDeadzone = 0.1f;
            mDynamicCenter = true;
            mHideWhenIdle = true;
        }

        private void Awake()
        {
            if (mOuter == null) mOuter = transform as RectTransform;
            if (mInner == null && transform.childCount > 0)
                mInner = transform.GetChild(0) as RectTransform;

            ApplyIdleVisibility();
            SetValue(Vector2.zero, forceSend: true);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (mOuter == null || mInner == null) return;

            IsPressed = true;
            mOnPressed?.Invoke();

            if (mDynamicCenter)
            {
                MovePadVisualCenterToScreen(eventData.position);
            }

            ApplyActiveVisibility();
            UpdateFromPointer(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsPressed) return;
            UpdateFromPointer(eventData.position);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!IsPressed) return;

            IsPressed = false;
            mOnReleased?.Invoke();

            SetValue(Vector2.zero, forceSend: true);
            SetInnerVisual(Vector2.zero);

            ApplyIdleVisibility();
        }

        private void UpdateFromPointer(Vector2 screenPos)
        {
            if (mOuter == null || mInner == null) return;

            var cam = ResolveUiCamera();

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(mOuter, screenPos, cam, out var localPoint))
                return;

            // pivot/anchor 변화에도 안전한 "비주얼 중심"
            var center = mOuter.rect.center;

            // local delta in pixels (rect local)
            var delta = localPoint - center;

            // Clamp to radius
            var clamped = Vector2.ClampMagnitude(delta, Mathf.Max(1f, mRadius));
            SetInnerVisual(clamped);

            // Normalize to -1..1
            var raw = clamped / Mathf.Max(1f, mRadius);

            // Deadzone
            var mag = raw.magnitude;
            if (mag <= mDeadzone)
            {
                SetValue(Vector2.zero);
                return;
            }

            // Rescale after deadzone so it reaches 1 at edge
            var scaledMag = (mag - mDeadzone) / Mathf.Max(0.0001f, 1f - mDeadzone);
            var value = raw.normalized * Mathf.Clamp01(scaledMag);

            SetValue(value);
        }

        private void SetValue(Vector2 v, bool forceSend = false)
        {
            CurrentValue = v;

            // avoid spamming identical values
            if (!forceSend && Approximately(mLastValueSent, v))
                return;

            mLastValueSent = v;
            mOnValueChanged?.Invoke(v);
        }

        private void SetInnerVisual(Vector2 deltaPixels)
        {
            if (mInner == null) return;
            mInner.anchoredPosition = deltaPixels;
        }

        private Camera ResolveUiCamera()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return null;
            return (canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;
        }

        private void MovePadVisualCenterToScreen(Vector2 screenPos)
        {
            if (mOuter == null) return;

            var parent = mOuter.parent as RectTransform;
            var cam = ResolveUiCamera();

            // outer 중심의 월드 좌표(현재)
            var centerWorld = mOuter.TransformPoint(mOuter.rect.center);
            var pivotWorld = mOuter.position;
            var pivotOffsetWorld = pivotWorld - centerWorld; // center->pivot offset

            if (parent != null &&
                RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, screenPos, cam, out var targetWorld))
            {
                // targetWorld는 parent 평면 위의 월드 위치(터치 지점).
                // outer의 "center"가 터치 지점에 오도록 pivot을 보정
                mOuter.position = targetWorld + pivotOffsetWorld;
            }
            else
            {
                // fallback
                mOuter.position = (Vector3)screenPos + pivotOffsetWorld;
            }

            if (mInner != null) mInner.anchoredPosition = Vector2.zero;
        }

        private void ApplyIdleVisibility()
        {
            if (!mHideWhenIdle) return;
            SetGraphicsEnabled(false);
        }

        private void ApplyActiveVisibility()
        {
            SetGraphicsEnabled(true);
        }

        private void SetGraphicsEnabled(bool enabled)
        {
            if (mOuter != null)
            {
                var g = mOuter.GetComponent<Graphic>();
                if (g != null) g.enabled = enabled;
            }

            if (mInner != null)
            {
                var g = mInner.GetComponent<Graphic>();
                if (g != null) g.enabled = enabled;
            }
        }

        private static bool Approximately(Vector2 a, Vector2 b)
        {
            return (a - b).sqrMagnitude <= 0.000001f;
        }
    }
}
