using System;
using UnityEngine;

namespace Devian
{
    [RequireComponent(typeof(UITransitionPlayer))]
    [RequireComponent(typeof(CanvasGroup))]
    public class UIPopupFrame : UIBaseFrame, IPoolable
    {
        [SerializeField] private UI_TRANSITION_PRESET_ID _openTransitionId;
        [SerializeField] private UI_TRANSITION_PRESET_ID _closeTransitionId;

        private UITransitionPlayer _transitionPlayer;
        private CanvasGroup _rootCanvasGroup;
        private Action<UIPopupFrame> _onOpened;
        private Action<UIPopupFrame, PopupCloseReason, object> _onCloseStarted;
        private Action<UIPopupFrame, PopupCloseReason, object> _onClosed;
        private PopupConfig _currentConfig;
        private PopupRequest _currentRequest;
        private object _pendingClosePayload;
        private PopupCloseReason _pendingCloseReason;
        private bool _isTop;

        public PopupFrameState state { get; private set; }
        public bool isTop => _isTop;

        protected PopupConfig currentConfig => _currentConfig;
        protected PopupRequest currentRequest => _currentRequest;

        protected override void onAwake()
        {
            ResolveDefaults();
        }

        protected override void onInit()
        {
            ResolveDefaults();
        }

        public void OnPoolSpawned()
        {
            ResolveDefaults();
            CancelInternal();
            _isTop = false;
        }

        public void OnPoolDespawned()
        {
            CancelInternal();
            _currentConfig = null;
            _currentRequest = default;
            _pendingClosePayload = null;
            _pendingCloseReason = PopupCloseReason.Canceled;
            _isTop = false;
        }

        internal void Open(
            PopupConfig config,
            PopupRequest request,
            Action<UIPopupFrame> onOpened,
            Action<UIPopupFrame, PopupCloseReason, object> onCloseStarted,
            Action<UIPopupFrame, PopupCloseReason, object> onClosed)
        {
            ResolveDefaults();
            CancelInternal();

            _currentConfig = config;
            _currentRequest = request;
            _onOpened = onOpened;
            _onCloseStarted = onCloseStarted;
            _onClosed = onClosed;
            _pendingClosePayload = null;
            _pendingCloseReason = PopupCloseReason.Canceled;
            state = PopupFrameState.Opening;

            onBind(request);
            ApplyTopState(_isTop);

            if (ShouldPlayOpenTransition())
            {
                var handle = _transitionPlayer.Play(_openTransitionId, HandleOpenCompleted);
                if (handle == null || handle.IsCanceled)
                {
                    HandleOpenCompleted();
                }

                return;
            }

            HandleOpenCompleted();
        }

        internal void CloseFromManager(PopupCloseReason reason, object payload = null)
        {
            BeginClose(reason, payload);
        }

        internal void SetTopState(bool isTop, bool allowInput)
        {
            _isTop = isTop;
            ApplyTopState(allowInput);
            onTopStateChanged(isTop, allowInput);
        }

        public void CloseCompleted()
        {
            BeginClose(PopupCloseReason.Completed, null);
        }

        public void CloseCanceled()
        {
            BeginClose(PopupCloseReason.Canceled, null);
        }

        protected void CloseWithResult(PopupCloseReason reason, object payload = null)
        {
            BeginClose(reason, payload);
        }

        protected virtual void onBind(PopupRequest request) { }
        protected virtual void onTopStateChanged(bool isTop, bool allowInput) { }

        private void BeginClose(PopupCloseReason reason, object payload)
        {
            if (state == PopupFrameState.Closing)
            {
                return;
            }

            ResolveDefaults();
            _transitionPlayer.Cancel();
            _pendingCloseReason = reason;
            _pendingClosePayload = payload;
            state = PopupFrameState.Closing;
            ApplyTopState(false);
            _onCloseStarted?.Invoke(this, reason, payload);

            if (ShouldPlayCloseTransition())
            {
                var handle = _transitionPlayer.Play(_closeTransitionId, CompleteClose);
                if (handle == null || handle.IsCanceled)
                {
                    CompleteClose();
                }

                return;
            }

            CompleteClose();
        }

        private void HandleOpenCompleted()
        {
            if (state != PopupFrameState.Opening)
            {
                return;
            }

            state = PopupFrameState.Opened;
            _onOpened?.Invoke(this);
        }

        private void CompleteClose()
        {
            if (state != PopupFrameState.Closing)
            {
                return;
            }

            var callback = _onClosed;
            callback?.Invoke(this, _pendingCloseReason, _pendingClosePayload);
        }

        private bool ShouldPlayOpenTransition()
        {
            return _currentConfig != null
                && _currentConfig.PlayOpenTransition
                && _openTransitionId != null
                && _openTransitionId.IsValid
                && _transitionPlayer != null;
        }

        private bool ShouldPlayCloseTransition()
        {
            return _currentConfig != null
                && _currentConfig.PlayCloseTransition
                && _closeTransitionId != null
                && _closeTransitionId.IsValid
                && _transitionPlayer != null;
        }

        private void ApplyTopState(bool allowInput)
        {
            if (_rootCanvasGroup == null)
            {
                return;
            }

            _rootCanvasGroup.interactable = allowInput;
            _rootCanvasGroup.blocksRaycasts = allowInput;
        }

        private void CancelInternal()
        {
            if (_transitionPlayer != null)
            {
                _transitionPlayer.Cancel();
            }

            _onOpened = null;
            _onCloseStarted = null;
            _onClosed = null;
        }

        private void ResolveDefaults()
        {
            if (_transitionPlayer == null)
            {
                _transitionPlayer = GetComponent<UITransitionPlayer>();
            }

            if (_rootCanvasGroup == null)
            {
                _rootCanvasGroup = GetComponent<CanvasGroup>();
            }
        }
    }
}
