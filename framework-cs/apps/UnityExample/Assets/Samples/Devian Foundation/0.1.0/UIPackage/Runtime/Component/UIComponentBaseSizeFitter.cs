using UnityEngine;

namespace Devian
{
    /// <summary>
    /// ExecuteAlways size-fitter family base.
    /// Restores baseline layout on disable/reload and centralizes refresh triggers.
    /// </summary>
    public abstract class UIComponentBaseSizeFitter : UIComponentBase
    {
        private struct ScreenObservation
        {
            public int Width;
            public int Height;
            public ScreenOrientation Orientation;
        }

        [SerializeField] private bool _refreshOnEnable = true;
        [SerializeField] private bool _refreshOnResolutionChange = true;
        [SerializeField] private bool _refreshOnOrientationChange = true;

        [SerializeField, HideInInspector] private RectTransform _baselineTarget;
        [SerializeField, HideInInspector] private Vector2 _baselineAnchorMin;
        [SerializeField, HideInInspector] private Vector2 _baselineAnchorMax;
        [SerializeField, HideInInspector] private Vector2 _baselineOffsetMin;
        [SerializeField, HideInInspector] private Vector2 _baselineOffsetMax;
        [SerializeField, HideInInspector] private Vector2 _baselineSizeDelta;
        [SerializeField, HideInInspector] private bool _hasBaseline;

        private int _lastObservedScreenWidth;
        private int _lastObservedScreenHeight;
        private ScreenOrientation _lastObservedOrientation;
        private bool _hasObservedScreenState;
        private bool _hasLoggedMultipleSizeFitterWarning;

        public RectTransform Target => transform as RectTransform;

        protected Canvas CurrentCanvas => canvas != null ? canvas : GetComponentInParent<Canvas>();

        protected sealed override void onAwake()
        {
            CaptureRefreshState();
            onSizeFitterAwake();
        }

        protected sealed override void onInit(Canvas canvas)
        {
            RestoreTrackedTargetToBaseline();
            ResetBaselineState();
            onSizeFitterInit(canvas);

            // 비활성화 상태에서는 rect를 건드리지 않는다.
            // OnEnable에서 Refresh가 실행된다.
            if (isActiveAndEnabled)
                Refresh();
        }

        protected virtual void onSizeFitterAwake() { }
        protected virtual void onSizeFitterInit(Canvas canvas) { }

        private void OnEnable()
        {
            if (Application.isPlaying && !isInitialized)
                return;

            if (!ShouldRunRefreshLoop())
                return;

            if (_refreshOnEnable || ShouldForceRefreshOnEnable())
            {
                Refresh();
                return;
            }

            CaptureRefreshState();
        }

        private void OnDisable()
        {
            RestoreTrackedTargetToBaseline();
            ResetTrackingState();
        }

        private void OnTransformParentChanged()
        {
            if (!isActiveAndEnabled)
                return;

            if (Application.isPlaying && !isInitialized)
                return;

            if (!ShouldRunRefreshLoop())
                return;

            RestoreTrackedTargetToBaseline();
            ResetBaselineState();
            Refresh();
        }

        private void OnValidate()
        {
            if (!isActiveAndEnabled)
                return;

            if (Application.isPlaying || ShouldRunEditorRefresh())
            {
                // OnValidate 중 RectTransform 변경 → SendMessage 경고 방지.
                // 다음 프레임으로 지연한다.
#if UNITY_EDITOR
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this != null && isActiveAndEnabled)
                        Refresh();
                };
#endif
            }
        }

        private void Update()
        {
            if (!ShouldRunRefreshLoop())
                return;

            if (HasRefreshTriggerChanged())
                Refresh();
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterDomainReloadHandler()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }

        private static void OnBeforeAssemblyReload()
        {
            foreach (var instance in Resources.FindObjectsOfTypeAll<UIComponentBaseSizeFitter>())
                instance.RestoreTrackedTargetToBaseline();
        }
#endif

        [ContextMenu("Refresh Size Fitter")]
        public void Refresh()
        {
            CaptureRefreshState();

            var target = Target;
            if (target == null)
                return;

            if (HasSiblingSizeFitterConflict())
                return;

            var currentCanvas = CurrentCanvas;
            if (currentCanvas != null && currentCanvas.renderMode == RenderMode.WorldSpace)
            {
                RestoreTrackedTargetToBaseline();
                ResetAppliedState();
                return;
            }

            EnsureBaseline(target);
            RestoreTrackedTargetToBaseline();
            ApplySizeFitter(currentCanvas, target);
        }

        [ContextMenu("Force Reset Baseline")]
        private void ForceResetBaseline()
        {
            _hasBaseline = false;
            onForceResetBaseline();
        }

        protected virtual void onForceResetBaseline() { }

        protected void EnsureBaseline(RectTransform target)
        {
            if (_hasBaseline && _baselineTarget == target)
                return;

            _baselineTarget = target;
            _baselineAnchorMin = target.anchorMin;
            _baselineAnchorMax = target.anchorMax;
            _baselineOffsetMin = target.offsetMin;
            _baselineOffsetMax = target.offsetMax;
            _baselineSizeDelta = target.sizeDelta;
            _hasBaseline = true;
        }

        protected void RestoreTrackedTargetToBaseline()
        {
            if (!_hasBaseline || _baselineTarget == null)
                return;

            _baselineTarget.anchorMin = _baselineAnchorMin;
            _baselineTarget.anchorMax = _baselineAnchorMax;
            _baselineTarget.offsetMin = _baselineOffsetMin;
            _baselineTarget.offsetMax = _baselineOffsetMax;
            _baselineTarget.sizeDelta = _baselineSizeDelta;
        }

        protected void ResetBaselineState()
        {
            _baselineTarget = null;
            _hasBaseline = false;
        }

        protected Vector2 ResolveReferenceRectSize(Canvas currentCanvas, RectTransform target)
        {
            var parentRect = target != null ? target.parent as RectTransform : null;
            if (parentRect != null)
            {
                var parentSize = parentRect.rect.size;
                if (parentSize.x > 0f && parentSize.y > 0f)
                    return parentSize;
            }

            if (currentCanvas != null)
            {
                var rootRect = currentCanvas.rootCanvas != null
                    ? currentCanvas.rootCanvas.transform as RectTransform
                    : currentCanvas.transform as RectTransform;
                if (rootRect != null)
                {
                    var canvasSize = rootRect.rect.size;
                    if (canvasSize.x > 0f && canvasSize.y > 0f)
                        return canvasSize;
                }

                var scaleFactor = Mathf.Max(currentCanvas.scaleFactor, 0.001f);
                return new Vector2(
                    Mathf.Max(1f, Screen.width / scaleFactor),
                    Mathf.Max(1f, Screen.height / scaleFactor));
            }

            return new Vector2(Mathf.Max(1f, Screen.width), Mathf.Max(1f, Screen.height));
        }

        protected static ScreenOrientation GetEffectiveOrientation()
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

        protected static bool AreSameRect(Rect lhs, Rect rhs)
        {
            return Mathf.Approximately(lhs.x, rhs.x)
                && Mathf.Approximately(lhs.y, rhs.y)
                && Mathf.Approximately(lhs.width, rhs.width)
                && Mathf.Approximately(lhs.height, rhs.height);
        }

        protected static bool AreSameVector2(Vector2 lhs, Vector2 rhs)
        {
            return Mathf.Approximately(lhs.x, rhs.x)
                && Mathf.Approximately(lhs.y, rhs.y);
        }

        protected abstract void ApplySizeFitter(Canvas currentCanvas, RectTransform target);

        protected virtual bool ShouldRunEditorRefresh() => false;
        protected virtual bool ShouldForceRefreshOnEnable() => false;
        protected virtual bool HasCustomRefreshTriggerChanged() => false;
        protected virtual void CaptureCustomRefreshState() { }
        protected virtual void ResetAppliedState() { }
        protected virtual void ResetCustomTrackingState() { }

        private bool ShouldRunRefreshLoop()
        {
            return Application.isPlaying || ShouldRunEditorRefresh();
        }

        private bool HasRefreshTriggerChanged()
        {
            if (!_hasObservedScreenState)
                return true;

            var current = ReadScreenObservation();
            if (_refreshOnResolutionChange
                && (_lastObservedScreenWidth != current.Width || _lastObservedScreenHeight != current.Height))
            {
                return true;
            }

            if (_refreshOnOrientationChange && _lastObservedOrientation != current.Orientation)
                return true;

            return HasCustomRefreshTriggerChanged();
        }

        private void CaptureRefreshState()
        {
            var current = ReadScreenObservation();
            _lastObservedScreenWidth = current.Width;
            _lastObservedScreenHeight = current.Height;
            _lastObservedOrientation = current.Orientation;
            _hasObservedScreenState = true;
            CaptureCustomRefreshState();
        }

        private void ResetTrackingState()
        {
            ResetBaselineState();
            ResetAppliedState();
            ResetCustomTrackingState();
            _hasObservedScreenState = false;
            _hasLoggedMultipleSizeFitterWarning = false;
        }

        private ScreenObservation ReadScreenObservation()
        {
            return new ScreenObservation
            {
                Width = Mathf.Max(1, Screen.width),
                Height = Mathf.Max(1, Screen.height),
                Orientation = GetEffectiveOrientation(),
            };
        }

        private bool HasSiblingSizeFitterConflict()
        {
            var fitters = GetComponents<UIComponentBaseSizeFitter>();
            if (fitters.Length <= 1)
            {
                _hasLoggedMultipleSizeFitterWarning = false;
                return false;
            }

            if (!_hasLoggedMultipleSizeFitterWarning)
            {
                Debug.LogWarning(
                    $"Only one {nameof(UIComponentBaseSizeFitter)} is supported per GameObject. " +
                    $"Move size fitters to separate parent/child nodes: {name}",
                    this);
                _hasLoggedMultipleSizeFitterWarning = true;
            }

            return true;
        }
    }
}
