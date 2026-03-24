using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Applies the current mobile safe area to a target RectTransform.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class UIComponentSafeArea : UIComponentBase
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

        [SerializeField] private RectTransform _target;
        [SerializeField] private bool _applyTop = true;
        [SerializeField] private bool _applyBottom = true;
        [SerializeField] private bool _applyLeft = true;
        [SerializeField] private bool _applyRight = true;
        [Tooltip("Additional inset in pixels. Positive values shrink the safe rect inward: left, bottom, right, top.")]
        [SerializeField] private Vector4 _extraPadding;
        [Tooltip("Anchor changes anchors. Offset keeps anchors and adjusts offsets from the cached baseline layout.")]
        [SerializeField] private SafeAreaApplyMode _applyMode = SafeAreaApplyMode.Anchor;
        [SerializeField] private bool _refreshOnEnable = true;
        [SerializeField] private bool _refreshOnResolutionChange = true;
        [SerializeField] private bool _refreshOnOrientationChange = true;
        [SerializeField] private bool _useEditorSimulation = true;
        [SerializeField] private SafeAreaEditorSimulationProfile _editorSimulationProfile = SafeAreaEditorSimulationProfile.IPhone14Pro;

        public RectTransform Target => ResolveTarget();
        public Rect LastAppliedSafeArea => _lastAppliedSafeArea;
        public ScreenOrientation LastOrientation => _lastAppliedOrientation;
        public bool IsApplied => _isApplied;

        private RectTransform _resolvedTarget;
        [SerializeField, HideInInspector] private RectTransform _baselineTarget;
        [SerializeField, HideInInspector] private Vector2 _baselineAnchorMin;
        [SerializeField, HideInInspector] private Vector2 _baselineAnchorMax;
        [SerializeField, HideInInspector] private Vector2 _baselineOffsetMin;
        [SerializeField, HideInInspector] private Vector2 _baselineOffsetMax;
        [SerializeField, HideInInspector] private bool _hasBaseline;
        private Rect _lastObservedSafeArea;
        private ScreenOrientation _lastObservedOrientation;
        private int _lastObservedScreenWidth;
        private int _lastObservedScreenHeight;
        private bool _hasObservedState;
        private Rect _lastAppliedSafeArea;
        private ScreenOrientation _lastAppliedOrientation;
        private bool _isApplied;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterDomainReloadHandler()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }

        private static void OnBeforeAssemblyReload()
        {
            foreach (var instance in Resources.FindObjectsOfTypeAll<UIComponentSafeArea>())
            {
                if (!instance._hasBaseline || instance._baselineTarget == null)
                {
                    continue;
                }

                instance._baselineTarget.anchorMin = instance._baselineAnchorMin;
                instance._baselineTarget.anchorMax = instance._baselineAnchorMax;
                instance._baselineTarget.offsetMin = instance._baselineOffsetMin;
                instance._baselineTarget.offsetMax = instance._baselineOffsetMax;
            }
        }
#endif

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

        protected override void onAwake()
        {
            ResolveTarget();
            CaptureObservedState(ReadCurrentObservation());
        }

        protected override void onInit(Canvas canvas)
        {
            RestoreTrackedTargetToBaseline();
            ResetBaselineState();
            Refresh();
        }

        private void OnEnable()
        {
            if (Application.isPlaying && !isInitialized)
            {
                return;
            }

            if (!ShouldRunRefreshLoop())
            {
                return;
            }

            if (_refreshOnEnable || ShouldUseEditorSimulation())
            {
                Refresh();
            }
            else
            {
                CaptureObservedState(ReadCurrentObservation());
            }
        }

        private void OnDisable()
        {
            RestoreTrackedTargetToBaseline();
            ResetTrackingState();
        }

        private void OnTransformParentChanged()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (Application.isPlaying && !isInitialized)
            {
                return;
            }

            if (!ShouldRunRefreshLoop())
            {
                return;
            }

            RestoreTrackedTargetToBaseline();
            ResetBaselineState();
            Refresh();
        }

        private void OnValidate()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (Application.isPlaying || ShouldUseEditorSimulation())
            {
                Refresh();
            }
        }

        private void Update()
        {
            if (!ShouldRunRefreshLoop())
            {
                return;
            }

            var observation = ReadCurrentObservation();
            if (!HasRefreshTriggerChanged(observation))
            {
                return;
            }

            RefreshInternal(observation);
        }

        [ContextMenu("Force Reset Baseline")]
        private void ForceResetBaseline()
        {
            _hasBaseline = false;
            _baselineTarget = null;
        }

        [ContextMenu("Refresh Safe Area")]
        public void Refresh()
        {
            RefreshInternal(ReadCurrentObservation());
        }

        private void RefreshInternal(SafeAreaObservation observation)
        {
            var target = ResolveTarget();
            CaptureObservedState(observation);

            if (target == null)
            {
                RestoreTrackedTargetToBaseline();
                ResetAppliedState();
                return;
            }

            var currentCanvas = canvas != null ? canvas : GetComponentInParent<Canvas>();
            if (currentCanvas != null && currentCanvas.renderMode == RenderMode.WorldSpace)
            {
                RestoreTrackedTargetToBaseline();
                ResetAppliedState();
                return;
            }

            EnsureBaseline(target);

            switch (_applyMode)
            {
                case SafeAreaApplyMode.Offset:
                    ApplyOffsetSafeArea(target, observation.SafeArea, observation.ScreenWidth, observation.ScreenHeight);
                    break;
                default:
                    ApplyAnchorSafeArea(target, observation.SafeArea, observation.ScreenWidth, observation.ScreenHeight);
                    break;
            }

            _lastAppliedSafeArea = observation.SafeArea;
            _lastAppliedOrientation = observation.Orientation;
            _isApplied = true;
        }

        private RectTransform ResolveTarget()
        {
            var resolvedTarget = _target != null ? _target : transform as RectTransform;
            if (_resolvedTarget != resolvedTarget)
            {
                RestoreTrackedTargetToBaseline();
                _resolvedTarget = resolvedTarget;
                ResetBaselineState();
            }

            return _resolvedTarget;
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

        private void EnsureBaseline(RectTransform target)
        {
            if (_hasBaseline && _baselineTarget == target)
            {
                return;
            }

            _baselineTarget = target;
            _baselineAnchorMin = target.anchorMin;
            _baselineAnchorMax = target.anchorMax;
            _baselineOffsetMin = target.offsetMin;
            _baselineOffsetMax = target.offsetMax;
            _hasBaseline = true;
        }

        private void ApplyAnchorSafeArea(RectTransform target, Rect safeArea, int screenWidth, int screenHeight)
        {
            var screenWidthFloat = Mathf.Max(1f, screenWidth);
            var screenHeightFloat = Mathf.Max(1f, screenHeight);
            var resolvedRect = ResolveSafeAreaRect(safeArea, screenWidthFloat, screenHeightFloat);

            target.anchorMin = new Vector2(resolvedRect.xMin / screenWidthFloat, resolvedRect.yMin / screenHeightFloat);
            target.anchorMax = new Vector2(resolvedRect.xMax / screenWidthFloat, resolvedRect.yMax / screenHeightFloat);
            target.offsetMin = _baselineOffsetMin;
            target.offsetMax = _baselineOffsetMax;
        }

        private void ApplyOffsetSafeArea(RectTransform target, Rect safeArea, int screenWidth, int screenHeight)
        {
            var screenWidthFloat = Mathf.Max(1f, screenWidth);
            var screenHeightFloat = Mathf.Max(1f, screenHeight);
            var resolvedRect = ResolveSafeAreaRect(safeArea, screenWidthFloat, screenHeightFloat);
            var offsetMin = _baselineOffsetMin;
            var offsetMax = _baselineOffsetMax;

            var currentCanvas = canvas != null ? canvas : GetComponentInParent<Canvas>();
            var scaleFactor = currentCanvas != null ? Mathf.Max(currentCanvas.scaleFactor, 0.001f) : 1f;
            var leftInset = resolvedRect.xMin / scaleFactor;
            var bottomInset = resolvedRect.yMin / scaleFactor;
            var rightInset = (screenWidthFloat - resolvedRect.xMax) / scaleFactor;
            var topInset = (screenHeightFloat - resolvedRect.yMax) / scaleFactor;

            var isFixedH = Mathf.Approximately(_baselineAnchorMin.x, _baselineAnchorMax.x);
            var isFixedV = Mathf.Approximately(_baselineAnchorMin.y, _baselineAnchorMax.y);

            ApplyHorizontalOffset(
                ref offsetMin.x,
                ref offsetMax.x,
                leftInset,
                rightInset,
                isFixedH);
            ApplyVerticalOffset(
                ref offsetMin.y,
                ref offsetMax.y,
                bottomInset,
                topInset,
                isFixedV);

            target.anchorMin = _baselineAnchorMin;
            target.anchorMax = _baselineAnchorMax;
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
                {
                    offsetMin += leftInset;
                }

                if (_applyRight)
                {
                    offsetMax -= rightInset;
                }

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
                {
                    offsetMin += bottomInset;
                }

                if (_applyTop)
                {
                    offsetMax -= topInset;
                }

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

        private bool HasRefreshTriggerChanged(SafeAreaObservation observation)
        {
            if (!_hasObservedState)
            {
                return true;
            }

            if (!AreSameRect(_lastObservedSafeArea, observation.SafeArea))
            {
                return true;
            }

            if (_refreshOnResolutionChange
                && (_lastObservedScreenWidth != observation.ScreenWidth || _lastObservedScreenHeight != observation.ScreenHeight))
            {
                return true;
            }

            if (_refreshOnOrientationChange && _lastObservedOrientation != observation.Orientation)
            {
                return true;
            }

            return false;
        }

        private void CaptureObservedState(SafeAreaObservation observation)
        {
            _lastObservedSafeArea = observation.SafeArea;
            _lastObservedOrientation = observation.Orientation;
            _lastObservedScreenWidth = observation.ScreenWidth;
            _lastObservedScreenHeight = observation.ScreenHeight;
            _hasObservedState = true;
        }

        private void RestoreTrackedTargetToBaseline()
        {
            if (!_hasBaseline || _baselineTarget == null)
            {
                return;
            }

            _baselineTarget.anchorMin = _baselineAnchorMin;
            _baselineTarget.anchorMax = _baselineAnchorMax;
            _baselineTarget.offsetMin = _baselineOffsetMin;
            _baselineTarget.offsetMax = _baselineOffsetMax;
        }

        private void ResetBaselineState()
        {
            _baselineTarget = null;
            _hasBaseline = false;
        }

        private void ResetAppliedState()
        {
            _lastAppliedSafeArea = default;
            _lastAppliedOrientation = default;
            _isApplied = false;
        }

        private void ResetTrackingState()
        {
            _resolvedTarget = null;
            ResetBaselineState();
            ResetAppliedState();
            _hasObservedState = false;
        }

        private bool ShouldRunRefreshLoop()
        {
            return Application.isPlaying || ShouldUseEditorSimulation();
        }

        private bool ShouldUseEditorSimulation()
        {
            return Application.isEditor
                && _useEditorSimulation
                && _editorSimulationProfile != SafeAreaEditorSimulationProfile.None;
        }

        private ScreenOrientation GetEffectiveOrientation()
        {
            switch (Screen.orientation)
            {
                case ScreenOrientation.Portrait:
                case ScreenOrientation.PortraitUpsideDown:
                case ScreenOrientation.LandscapeLeft:
                case ScreenOrientation.LandscapeRight:
                    return Screen.orientation;
                default:
                    return Screen.width >= Screen.height
                        ? ScreenOrientation.LandscapeLeft
                        : ScreenOrientation.Portrait;
            }
        }

        private ISafeAreaSource ResolveSafeAreaSource()
        {
            if (!ShouldUseEditorSimulation())
            {
                return s_RuntimeSafeAreaSource;
            }

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

        private static bool AreSameRect(Rect lhs, Rect rhs)
        {
            return Mathf.Approximately(lhs.x, rhs.x)
                && Mathf.Approximately(lhs.y, rhs.y)
                && Mathf.Approximately(lhs.width, rhs.width)
                && Mathf.Approximately(lhs.height, rhs.height);
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
