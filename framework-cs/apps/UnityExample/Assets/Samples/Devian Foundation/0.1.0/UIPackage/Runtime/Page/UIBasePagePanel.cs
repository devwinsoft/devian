using System;
using UnityEngine;

namespace Devian
{
    public enum UIPageState
    {
        Hidden,
        Entering,
        Visible,
        Exiting
    }

    public enum UIPageTransitionDirection
    {
        None,
        Left,
        Right
    }

    [RequireComponent(typeof(UITransitionPlayer))]
    public abstract class UIBasePagePanel : UIBasePanel
    {
        [SerializeField] private int _pageIndex;
        [SerializeField] private UI_TRANSITION_PRESET_ID _enterFromLeftTransitionId;
        [SerializeField] private UI_TRANSITION_PRESET_ID _enterFromRightTransitionId;
        [SerializeField] private UI_TRANSITION_PRESET_ID _exitToLeftTransitionId;
        [SerializeField] private UI_TRANSITION_PRESET_ID _exitToRightTransitionId;

        private UITransitionPlayer _transitionPlayer;
        private UITweenHandle _transitionHandle;

        public int pageIndex => _pageIndex;
        public bool isCurrentPage { get; private set; }
        public UIPageState pageState { get; private set; } = UIPageState.Hidden;
        public UIBasePageCanvas ownerCanvas => ownerBase as UIBasePageCanvas;

        protected sealed override void onInitFromCanvas(MonoBehaviour canvasOwner)
        {
            var pageCanvas = canvasOwner as UIBasePageCanvas;
            if (pageCanvas == null)
            {
                Debug.LogError(
                    $"[{GetType().Name}] Page owner must be UIBasePageCanvas. " +
                    $"Actual type: {canvasOwner?.GetType().Name ?? "null"}",
                    this);
                return;
            }

            onInit(pageCanvas);
        }

        protected virtual void onInit(UIBasePageCanvas canvas) { }

        protected virtual void onPageWillEnter(UIPageTransitionDirection direction, UIBasePagePanel fromPage) { }
        protected virtual void onPageEntered(UIPageTransitionDirection direction, UIBasePagePanel fromPage) { }
        protected virtual void onPageWillExit(UIPageTransitionDirection direction, UIBasePagePanel toPage) { }
        protected virtual void onPageExited(UIPageTransitionDirection direction, UIBasePagePanel toPage) { }

        internal void _Enter(
            UIPageTransitionDirection direction,
            bool animated,
            UIBasePagePanel fromPage,
            Action onComplete)
        {
            CancelTransitionInternal();

            isCurrentPage = true;
            if (!gameObject.activeSelf || !isShown)
                Show();

            pageState = UIPageState.Entering;
            onPageWillEnter(direction, fromPage);

            if (!TryPlayTransition(direction, isEnter: true, animated, () =>
                CompleteEnter(direction, fromPage, onComplete)))
            {
                CompleteEnter(direction, fromPage, onComplete);
            }
        }

        internal void _Exit(
            UIPageTransitionDirection direction,
            bool animated,
            UIBasePagePanel toPage,
            Action onComplete)
        {
            CancelTransitionInternal();

            isCurrentPage = false;
            pageState = UIPageState.Exiting;
            onPageWillExit(direction, toPage);

            if (!TryPlayTransition(direction, isEnter: false, animated, () =>
                CompleteExit(direction, toPage, onComplete)))
            {
                CompleteExit(direction, toPage, onComplete);
            }
        }

        internal void _NormalizeVisible(bool makeCurrent)
        {
            CancelTransitionInternal();
            isCurrentPage = makeCurrent;

            if (!gameObject.activeSelf || !isShown)
                Show();

            pageState = UIPageState.Visible;
        }

        internal void _NormalizeHidden()
        {
            CancelTransitionInternal();
            isCurrentPage = false;
            pageState = UIPageState.Hidden;

            if (gameObject.activeSelf || isShown)
                Hide();
        }

        internal void _CancelPageTransition()
        {
            CancelTransitionInternal();
        }

        internal bool _ValidateTransitionSetup(out string reason)
        {
            if (ResolveTransitionPlayer() == null)
            {
                reason = $"Page '{name}' is missing UITransitionPlayer.";
                return false;
            }

            reason = null;
            return true;
        }

        private void CompleteEnter(
            UIPageTransitionDirection direction,
            UIBasePagePanel fromPage,
            Action onComplete)
        {
            _transitionHandle = null;
            isCurrentPage = true;
            pageState = UIPageState.Visible;
            onPageEntered(direction, fromPage);
            onComplete?.Invoke();
        }

        private void CompleteExit(
            UIPageTransitionDirection direction,
            UIBasePagePanel toPage,
            Action onComplete)
        {
            _transitionHandle = null;
            isCurrentPage = false;
            Hide();
            pageState = UIPageState.Hidden;
            onPageExited(direction, toPage);
            onComplete?.Invoke();
        }

        private bool TryPlayTransition(
            UIPageTransitionDirection direction,
            bool isEnter,
            bool animated,
            Action onComplete)
        {
            if (!animated)
                return false;

            var presetId = ResolveTransitionId(direction, isEnter);
            if (presetId == null || !presetId.IsValid)
                return false;

            var transitionPlayer = ResolveTransitionPlayer();
            if (transitionPlayer == null)
                return false;

            var handle = transitionPlayer.Play(presetId, onComplete);
            if (handle == null || handle.IsCanceled)
            {
                _transitionHandle = null;
                return false;
            }

            _transitionHandle = handle;
            return true;
        }

        private UI_TRANSITION_PRESET_ID ResolveTransitionId(
            UIPageTransitionDirection direction,
            bool isEnter)
        {
            switch (direction)
            {
                case UIPageTransitionDirection.Left:
                    return isEnter
                        ? _enterFromLeftTransitionId
                        : _exitToLeftTransitionId;

                case UIPageTransitionDirection.Right:
                    return isEnter
                        ? _enterFromRightTransitionId
                        : _exitToRightTransitionId;

                default:
                    return null;
            }
        }

        private void CancelTransitionInternal()
        {
            if (_transitionHandle != null)
            {
                _transitionHandle.Cancel();
                _transitionHandle = null;
            }

            var transitionPlayer = ResolveTransitionPlayer();
            if (transitionPlayer != null)
                transitionPlayer.Cancel();
        }

        private UITransitionPlayer ResolveTransitionPlayer()
        {
            if (_transitionPlayer == null)
                _transitionPlayer = GetComponent<UITransitionPlayer>();

            return _transitionPlayer;
        }
    }

    public abstract class UIBasePagePanel<TCanvas> : UIBasePagePanel
        where TCanvas : UIBasePageCanvas
    {
        protected new TCanvas ownerCanvas { get; private set; }

        protected sealed override void onInit(UIBasePageCanvas canvas)
        {
            ownerCanvas = canvas as TCanvas;
            if (ownerCanvas == null)
            {
                Debug.LogError(
                    $"[{GetType().Name}] Page canvas must be {typeof(TCanvas).Name}. " +
                    $"Actual type: {canvas?.GetType().Name ?? "null"}",
                    this);
                return;
            }

            onInit(ownerCanvas);
        }

        protected virtual void onInit(TCanvas canvas) { }
    }
}
