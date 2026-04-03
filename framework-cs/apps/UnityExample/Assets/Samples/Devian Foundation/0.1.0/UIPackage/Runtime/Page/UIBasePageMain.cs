using System;
using UnityEngine;

namespace Devian
{
    [RequireComponent(typeof(UITransitionPlayer))]
    public abstract class UIBasePageMain : UIBasePagePanel
    {
        [SerializeField] private UI_TRANSITION_PRESET_ID _enterFromLeftTransitionId;
        [SerializeField] private UI_TRANSITION_PRESET_ID _enterFromRightTransitionId;
        [SerializeField] private UI_TRANSITION_PRESET_ID _exitToLeftTransitionId;
        [SerializeField] private UI_TRANSITION_PRESET_ID _exitToRightTransitionId;

        private UITransitionPlayer _transitionPlayer;
        private UITweenHandle _transitionHandle;

        public bool isCurrentMain { get; private set; }
        public UIPageState pageState { get; private set; } = UIPageState.Hidden;

        protected virtual void onPageWillEnter(UIPageTransitionDirection direction, UIBasePageMain fromPage) { }
        protected virtual void onPageEntered(UIPageTransitionDirection direction, UIBasePageMain fromPage) { }
        protected virtual void onPageWillExit(UIPageTransitionDirection direction, UIBasePageMain toPage) { }
        protected virtual void onPageExited(UIPageTransitionDirection direction, UIBasePageMain toPage) { }

        internal void _Enter(
            UIPageTransitionDirection direction,
            bool animated,
            UIBasePageMain fromPage,
            Action onComplete)
        {
            CancelTransitionInternal();

            isCurrentMain = true;
            _SetShownImmediate(true);

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
            UIBasePageMain toPage,
            Action onComplete)
        {
            CancelTransitionInternal();

            isCurrentMain = false;
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
            isCurrentMain = makeCurrent;
            _SetShownImmediate(true);
            pageState = UIPageState.Visible;
        }

        internal void _NormalizeHidden()
        {
            CancelTransitionInternal();
            isCurrentMain = false;
            pageState = UIPageState.Hidden;
            _SetShownImmediate(false);
        }

        internal void _CancelPageTransition()
        {
            CancelTransitionInternal();
        }

        internal bool _ValidateTransitionSetup(out string reason)
        {
            if (ResolveTransitionPlayer() == null)
            {
                reason = $"Main page '{name}' is missing UITransitionPlayer.";
                return false;
            }

            reason = null;
            return true;
        }

        private void CompleteEnter(
            UIPageTransitionDirection direction,
            UIBasePageMain fromPage,
            Action onComplete)
        {
            _transitionHandle = null;
            isCurrentMain = true;
            pageState = UIPageState.Visible;
            onPageEntered(direction, fromPage);
            onComplete?.Invoke();
        }

        private void CompleteExit(
            UIPageTransitionDirection direction,
            UIBasePageMain toPage,
            Action onComplete)
        {
            _transitionHandle = null;
            isCurrentMain = false;
            _SetShownImmediate(false);
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

    public abstract class UIBasePageMain<TCanvas> : UIBasePageMain
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
