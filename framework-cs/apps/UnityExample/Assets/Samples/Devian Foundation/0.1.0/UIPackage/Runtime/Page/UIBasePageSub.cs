using System;
using UnityEngine;

namespace Devian
{
    [RequireComponent(typeof(UITransitionPlayer))]
    public abstract class UIBasePageSub : UIBasePagePanel
    {
        [SerializeField] private UI_TRANSITION_PRESET_ID _showTransitionId;
        [SerializeField] private UI_TRANSITION_PRESET_ID _hideTransitionId;

        private UITransitionPlayer _transitionPlayer;
        private UITweenHandle _transitionHandle;

        public bool isCurrentSub => ownerCanvas != null && ownerCanvas.currentSub == this;

        public new void Show()
        {
            if (ownerCanvas == null)
            {
                _ShowInternal(animated: false, null);
                return;
            }

            ownerCanvas.TryShowSub(this);
        }

        public new void Hide()
        {
            if (ownerCanvas == null)
            {
                _HideInternal(animated: false, null);
                return;
            }

            ownerCanvas.TryHideSub(this, UIPageSubCloseReason.Normal, animated: true);
        }

        internal void _ShowInternal(bool animated, Action onComplete)
        {
            CancelTransitionInternal();
            _SetShownImmediate(true);

            if (!TryPlayTransition(_showTransitionId, animated, () =>
                CompleteShow(onComplete)))
            {
                CompleteShow(onComplete);
            }
        }

        internal void _HideInternal(bool animated, Action onComplete)
        {
            if (!gameObject.activeSelf && !isShown)
            {
                _SetShownImmediate(false);
                onComplete?.Invoke();
                return;
            }

            CancelTransitionInternal();

            if (!TryPlayTransition(_hideTransitionId, animated, () =>
                CompleteHide(onComplete)))
            {
                CompleteHide(onComplete);
            }
        }

        internal void _NormalizeHidden()
        {
            CancelTransitionInternal();
            _SetShownImmediate(false);
        }

        internal void _NormalizeVisible()
        {
            CancelTransitionInternal();
            _SetShownImmediate(true);
        }

        internal void _CancelTransition()
        {
            CancelTransitionInternal();
        }

        internal bool _ValidateTransitionSetup(out string reason)
        {
            if (ResolveTransitionPlayer() == null)
            {
                reason = $"Sub page '{name}' is missing UITransitionPlayer.";
                return false;
            }

            reason = null;
            return true;
        }

        private void CompleteShow(Action onComplete)
        {
            _transitionHandle = null;
            onComplete?.Invoke();
        }

        private void CompleteHide(Action onComplete)
        {
            _transitionHandle = null;
            _SetShownImmediate(false);
            onComplete?.Invoke();
        }

        private bool TryPlayTransition(
            UI_TRANSITION_PRESET_ID presetId,
            bool animated,
            Action onComplete)
        {
            if (!animated)
                return false;

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

    public abstract class UIBasePageSub<TCanvas> : UIBasePageSub
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
