using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Applies the current mobile safe area to its own RectTransform.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UIComponentSafeSizeFitter : UIComponentBaseSizeFitter
    {
        private struct SafeAreaObservation
        {
            public Rect SafeArea;
            public ScreenOrientation Orientation;
            public int ScreenWidth;
            public int ScreenHeight;
        }

        public enum SafeAreaApplyMode
        {
            Anchor,
            Offset,
        }

        public enum SafeAreaEditorSimulationProfile
        {
            None,
            IPhone14Pro,
            IPad,
            AndroidTall,
        }

        [SerializeField] private bool _applyTop = true;
        [SerializeField] private bool _applyBottom = true;
        [SerializeField] private bool _applyLeft = true;
        [SerializeField] private bool _applyRight = true;
        [Tooltip("Additional inset in pixels. Positive values shrink the safe rect inward: left, bottom, right, top.")]
        [SerializeField] private Vector4 _extraPadding;
        [Tooltip("Anchor changes anchors. Offset keeps anchors and adjusts offsets from the cached baseline layout.")]
        [SerializeField] private SafeAreaApplyMode _applyMode = SafeAreaApplyMode.Anchor;
        [SerializeField] private bool _useEditorSimulation = true;
        [SerializeField] private SafeAreaEditorSimulationProfile _editorSimulationProfile = SafeAreaEditorSimulationProfile.IPhone14Pro;

        public Rect LastAppliedSafeArea => _lastAppliedSafeArea;
        public ScreenOrientation LastOrientation => _lastAppliedOrientation;
        public bool IsApplied => _isApplied;

        private Rect _lastObservedSafeArea;
        private Rect _lastAppliedSafeArea;
        private ScreenOrientation _lastAppliedOrientation;
        private bool _isApplied;

        private static readonly ISafeAreaSource s_RuntimeSafeAreaSource = new RuntimeSafeAreaSource();
        private static readonly ISafeAreaSource s_IPhone14ProSource =
            new EditorSimulationSafeAreaSource(
                new SafeAreaInsets(0f, 34f, 0f, 59f),
                new SafeAreaInsets(59f, 21f, 59f, 0f));
        private static readonly ISafeAreaSource s_IPadSource =
            new EditorSimulationSafeAreaSource(
                new SafeAreaInsets(0f, 20f, 0f, 24f),
                new SafeAreaInsets(0f, 20f, 0f, 24f));
        private static readonly ISafeAreaSource s_AndroidTallSource =
            new EditorSimulationSafeAreaSource(
                new SafeAreaInsets(0f, 16f, 0f, 32f),
                new SafeAreaInsets(24f, 0f, 24f, 0f));

        protected override bool ShouldRunEditorRefresh()
        {
            return ShouldUseEditorSimulation();
        }

        protected override bool ShouldForceRefreshOnEnable()
        {
            return ShouldUseEditorSimulation();
        }

        protected override void ApplySizeFitter(Canvas currentCanvas, RectTransform target)
        {
            var observation = ReadCurrentObservation();

            switch (_applyMode)
            {
                case SafeAreaApplyMode.Offset:
                    ApplyOffsetSafeArea(target, observation.SafeArea, observation.ScreenWidth, observation.ScreenHeight, currentCanvas);
                    break;
                default:
                    ApplyAnchorSafeArea(target, observation.SafeArea, observation.ScreenWidth, observation.ScreenHeight);
                    break;
            }

            _lastAppliedSafeArea = observation.SafeArea;
            _lastAppliedOrientation = observation.Orientation;
            _isApplied = true;
        }

        protected override bool HasCustomRefreshTriggerChanged()
        {
            return !AreSameRect(_lastObservedSafeArea, ReadCurrentObservation().SafeArea);
        }

        protected override void CaptureCustomRefreshState()
        {
            _lastObservedSafeArea = ReadCurrentObservation().SafeArea;
        }

        protected override void ResetAppliedState()
        {
            _lastAppliedSafeArea = default;
            _lastAppliedOrientation = default;
            _isApplied = false;
        }

        protected override void ResetCustomTrackingState()
        {
            _lastObservedSafeArea = default;
        }

        private SafeAreaObservation ReadCurrentObservation()
        {
            var screenWidth = Mathf.Max(1, Screen.width);
            var screenHeight = Mathf.Max(1, Screen.height);
            var orientation = GetEffectiveOrientation();
            return new SafeAreaObservation
            {
                SafeArea = ResolveSafeAreaSource().GetSafeArea(screenWidth, screenHeight, orientation),
                Orientation = orientation,
                ScreenWidth = screenWidth,
                ScreenHeight = screenHeight,
            };
        }

        private void ApplyAnchorSafeArea(RectTransform target, Rect safeArea, int screenWidth, int screenHeight)
        {
            var screenWidthFloat = Mathf.Max(1f, screenWidth);
            var screenHeightFloat = Mathf.Max(1f, screenHeight);
            var resolvedRect = ResolveSafeAreaRect(safeArea, screenWidthFloat, screenHeightFloat);

            target.anchorMin = new Vector2(resolvedRect.xMin / screenWidthFloat, resolvedRect.yMin / screenHeightFloat);
            target.anchorMax = new Vector2(resolvedRect.xMax / screenWidthFloat, resolvedRect.yMax / screenHeightFloat);
        }

        private void ApplyOffsetSafeArea(
            RectTransform target,
            Rect safeArea,
            int screenWidth,
            int screenHeight,
            Canvas currentCanvas)
        {
            var screenWidthFloat = Mathf.Max(1f, screenWidth);
            var screenHeightFloat = Mathf.Max(1f, screenHeight);
            var resolvedRect = ResolveSafeAreaRect(safeArea, screenWidthFloat, screenHeightFloat);
            var offsetMin = target.offsetMin;
            var offsetMax = target.offsetMax;
            var scaleFactor = currentCanvas != null ? Mathf.Max(currentCanvas.scaleFactor, 0.001f) : 1f;
            var leftInset = resolvedRect.xMin / scaleFactor;
            var bottomInset = resolvedRect.yMin / scaleFactor;
            var rightInset = (screenWidthFloat - resolvedRect.xMax) / scaleFactor;
            var topInset = (screenHeightFloat - resolvedRect.yMax) / scaleFactor;
            var isFixedH = Mathf.Approximately(target.anchorMin.x, target.anchorMax.x);
            var isFixedV = Mathf.Approximately(target.anchorMin.y, target.anchorMax.y);

            ApplyHorizontalOffset(ref offsetMin.x, ref offsetMax.x, leftInset, rightInset, isFixedH);
            ApplyVerticalOffset(ref offsetMin.y, ref offsetMax.y, bottomInset, topInset, isFixedV);

            target.offsetMin = offsetMin;
            target.offsetMax = offsetMax;
        }

        private Rect ResolveSafeAreaRect(Rect safeArea, float screenWidth, float screenHeight)
        {
            var left = _applyLeft ? safeArea.xMin + _extraPadding.x : 0f;
            var bottom = _applyBottom ? safeArea.yMin + _extraPadding.y : 0f;
            var right = _applyRight ? safeArea.xMax - _extraPadding.z : screenWidth;
            var top = _applyTop ? safeArea.yMax - _extraPadding.w : screenHeight;

            left = Mathf.Clamp(left, 0f, screenWidth);
            bottom = Mathf.Clamp(bottom, 0f, screenHeight);
            right = Mathf.Clamp(right, left, screenWidth);
            top = Mathf.Clamp(top, bottom, screenHeight);
            return Rect.MinMaxRect(left, bottom, right, top);
        }

        private void ApplyHorizontalOffset(
            ref float offsetMin,
            ref float offsetMax,
            float leftInset,
            float rightInset,
            bool isFixedAxis)
        {
            if (!isFixedAxis)
            {
                if (_applyLeft)
                    offsetMin += leftInset;

                if (_applyRight)
                    offsetMax -= rightInset;

                return;
            }

            if (_applyLeft && _applyRight)
            {
                offsetMin += leftInset;
                offsetMax -= rightInset;
                return;
            }

            if (_applyLeft)
            {
                offsetMin += leftInset;
                offsetMax += leftInset;
                return;
            }

            if (_applyRight)
            {
                offsetMin -= rightInset;
                offsetMax -= rightInset;
            }
        }

        private void ApplyVerticalOffset(
            ref float offsetMin,
            ref float offsetMax,
            float bottomInset,
            float topInset,
            bool isFixedAxis)
        {
            if (!isFixedAxis)
            {
                if (_applyBottom)
                    offsetMin += bottomInset;

                if (_applyTop)
                    offsetMax -= topInset;

                return;
            }

            if (_applyBottom && _applyTop)
            {
                offsetMin += bottomInset;
                offsetMax -= topInset;
                return;
            }

            if (_applyBottom)
            {
                offsetMin += bottomInset;
                offsetMax += bottomInset;
                return;
            }

            if (_applyTop)
            {
                offsetMin -= topInset;
                offsetMax -= topInset;
            }
        }

        private bool ShouldUseEditorSimulation()
        {
            return Application.isEditor
                && _useEditorSimulation
                && _editorSimulationProfile != SafeAreaEditorSimulationProfile.None;
        }

        private ISafeAreaSource ResolveSafeAreaSource()
        {
            if (!ShouldUseEditorSimulation())
                return s_RuntimeSafeAreaSource;

            switch (_editorSimulationProfile)
            {
                case SafeAreaEditorSimulationProfile.IPhone14Pro:
                    return s_IPhone14ProSource;
                case SafeAreaEditorSimulationProfile.IPad:
                    return s_IPadSource;
                case SafeAreaEditorSimulationProfile.AndroidTall:
                    return s_AndroidTallSource;
                default:
                    return s_RuntimeSafeAreaSource;
            }
        }

        private interface ISafeAreaSource
        {
            Rect GetSafeArea(int screenWidth, int screenHeight, ScreenOrientation orientation);
        }

        private sealed class RuntimeSafeAreaSource : ISafeAreaSource
        {
            public Rect GetSafeArea(int screenWidth, int screenHeight, ScreenOrientation orientation)
            {
                return Screen.safeArea;
            }
        }

        private sealed class EditorSimulationSafeAreaSource : ISafeAreaSource
        {
            private readonly SafeAreaInsets _portraitInsets;
            private readonly SafeAreaInsets _landscapeInsets;

            public EditorSimulationSafeAreaSource(SafeAreaInsets portraitInsets, SafeAreaInsets landscapeInsets)
            {
                _portraitInsets = portraitInsets;
                _landscapeInsets = landscapeInsets;
            }

            public Rect GetSafeArea(int screenWidth, int screenHeight, ScreenOrientation orientation)
            {
                var insets = IsLandscape(orientation) ? _landscapeInsets : _portraitInsets;
                var left = Mathf.Clamp(insets.Left, 0f, screenWidth);
                var bottom = Mathf.Clamp(insets.Bottom, 0f, screenHeight);
                var right = Mathf.Clamp(screenWidth - insets.Right, left, screenWidth);
                var top = Mathf.Clamp(screenHeight - insets.Top, bottom, screenHeight);
                return Rect.MinMaxRect(left, bottom, right, top);
            }

            private static bool IsLandscape(ScreenOrientation orientation)
            {
                return orientation == ScreenOrientation.LandscapeLeft
                    || orientation == ScreenOrientation.LandscapeRight;
            }
        }

        private readonly struct SafeAreaInsets
        {
            public readonly float Left;
            public readonly float Bottom;
            public readonly float Right;
            public readonly float Top;

            public SafeAreaInsets(float left, float bottom, float right, float top)
            {
                Left = left;
                Bottom = bottom;
                Right = right;
                Top = top;
            }
        }
    }
}
