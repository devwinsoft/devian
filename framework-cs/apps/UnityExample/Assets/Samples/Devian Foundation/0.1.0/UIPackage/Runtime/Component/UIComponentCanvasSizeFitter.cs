using UnityEngine;
using UnityEngine.UI;

namespace Devian
{
    /// <summary>
    /// Fits an Image to the available canvas rect while preserving sprite aspect ratio.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [RequireComponent(typeof(Image))]
    public sealed class UIComponentCanvasSizeFitter : UIComponentBaseSizeFitter
    {
        public enum CanvasSizeFitMode
        {
            Inner,
            Outer,
        }

        [SerializeField] private CanvasSizeFitMode _fitMode = CanvasSizeFitMode.Inner;

        public Vector2 LastAppliedSize => _lastAppliedSize;
        public bool IsApplied => _isApplied;

        private Image _image;
        private EntityId _lastObservedSpriteEntityId;
        private Vector2 _lastObservedReferenceSize;
        private Vector2 _lastAppliedSize;
        private bool _isApplied;

        protected override void onSizeFitterAwake()
        {
            _image = GetComponent<Image>();
        }

        protected override bool ShouldRunEditorRefresh()
        {
            return true;
        }

        protected override void ApplySizeFitter(Canvas currentCanvas, RectTransform target)
        {
            if (!TryGetSpriteNativeSize(currentCanvas, out var spriteNativeSize))
            {
                RestoreTrackedTargetToBaseline();
                ResetAppliedState();
                return;
            }

            var referenceSize = ResolveReferenceRectSize(currentCanvas, target);
            if (referenceSize.x <= 0f || referenceSize.y <= 0f)
            {
                RestoreTrackedTargetToBaseline();
                ResetAppliedState();
                return;
            }

            var scaleX = referenceSize.x / spriteNativeSize.x;
            var scaleY = referenceSize.y / spriteNativeSize.y;
            var scale = _fitMode == CanvasSizeFitMode.Outer
                ? Mathf.Max(scaleX, scaleY)
                : Mathf.Min(scaleX, scaleY);

            var fittedSize = spriteNativeSize * Mathf.Max(scale, 0f);
            target.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, fittedSize.x);
            target.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, fittedSize.y);

            _lastAppliedSize = fittedSize;
            _isApplied = true;
        }

        protected override bool HasCustomRefreshTriggerChanged()
        {
            return _lastObservedSpriteEntityId != GetCurrentSpriteEntityId()
                || !AreSameVector2(_lastObservedReferenceSize, ResolveCurrentReferenceSize());
        }

        protected override void CaptureCustomRefreshState()
        {
            _lastObservedSpriteEntityId = GetCurrentSpriteEntityId();
            _lastObservedReferenceSize = ResolveCurrentReferenceSize();
        }

        protected override void ResetAppliedState()
        {
            _lastAppliedSize = default;
            _isApplied = false;
        }

        protected override void ResetCustomTrackingState()
        {
            _lastObservedSpriteEntityId = default;
            _lastObservedReferenceSize = default;
        }

        private EntityId GetCurrentSpriteEntityId()
        {
            var sprite = ResolveSprite();
            return sprite != null ? sprite.GetEntityId() : default;
        }

        private Vector2 ResolveCurrentReferenceSize()
        {
            var target = Target;
            return ResolveReferenceRectSize(CurrentCanvas, target);
        }

        private Sprite ResolveSprite()
        {
            if (_image == null)
                _image = GetComponent<Image>();

            if (_image == null)
                return null;

            return _image.overrideSprite != null ? _image.overrideSprite : _image.sprite;
        }

        private bool TryGetSpriteNativeSize(Canvas currentCanvas, out Vector2 size)
        {
            var sprite = ResolveSprite();
            if (sprite == null)
            {
                size = default;
                return false;
            }

            var spritePixelsPerUnit = Mathf.Max(sprite.pixelsPerUnit, 0.001f);
            var referencePixelsPerUnit = currentCanvas != null
                ? Mathf.Max(currentCanvas.referencePixelsPerUnit, 0.001f)
                : 100f;
            size = sprite.rect.size * (referencePixelsPerUnit / spritePixelsPerUnit);
            return size.x > 0f && size.y > 0f;
        }
    }
}
