using System;
using UnityEngine;

namespace Devian
{
    [RequireComponent(typeof(UITransitionPlayer))]
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIPopupFrameBase : UIBaseFrame, IPoolable
    {
        [SerializeField] private UI_TRANSITION_PRESET_ID _showTransitionId;
        [SerializeField] private UI_TRANSITION_PRESET_ID _closeTransitionId;

        private UITransitionPlayer _transitionPlayer;
        private CanvasGroup _rootCanvasGroup;
        private Action<UIPopupFrameBase> _onShow;
        private Action<UIPopupFrameBase, PopupCloseReason> _onCloseStarted;
        private Action<UIPopupFrameBase, PopupCloseReason> _onClosed;
        private object _currentPayload;
        private PopupCloseReason _pendingCloseReason;
        private bool _isTop;

        public PopupFrameState State { get; private set; }
        public PopupFrameState state => State;
        public bool IsTop => _isTop;
        public bool isTop => _isTop;

        protected object currentPayload => _currentPayload;

        protected virtual bool UseDim => UIPopupDefaults.DefaultUseDim;
        protected virtual bool BlockInputBehind => UIPopupDefaults.DefaultBlockInputBehind;
        protected virtual bool CloseOnBack => UIPopupDefaults.DefaultCloseOnBack;
        protected virtual bool CloseOnEscape => UIPopupDefaults.DefaultCloseOnEscape;
        protected virtual bool CloseOnDimClick => UIPopupDefaults.DefaultCloseOnDimClick;
        protected virtual PopupDuplicatePolicy DuplicatePolicy => UIPopupDefaults.DefaultDuplicatePolicy;
        protected virtual bool PlayShowTransition => UIPopupDefaults.DefaultPlayShowTransition;
        protected virtual bool PlayCloseTransition => UIPopupDefaults.DefaultPlayCloseTransition;

        internal bool useDim => UseDim;
        internal bool blockInputBehind => BlockInputBehind;
        internal bool closeOnBack => CloseOnBack;
        internal bool closeOnEscape => CloseOnEscape;
        internal bool closeOnDimClick => CloseOnDimClick;
        internal PopupDuplicatePolicy duplicatePolicy => DuplicatePolicy;

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
            _currentPayload = null;
            _pendingCloseReason = PopupCloseReason.Cancel;
            _isTop = false;
        }

        public void OnPoolDespawned()
        {
            CancelInternal();
            _currentPayload = null;
            _pendingCloseReason = PopupCloseReason.Cancel;
            _isTop = false;
        }

        internal void ShowUntyped(
            object payload,
            Action<UIPopupFrameBase> onShow,
            Action<UIPopupFrameBase, PopupCloseReason> onCloseStarted,
            Action<UIPopupFrameBase, PopupCloseReason> onClosed)
        {
            ResolveDefaults();
            CancelInternal();

            _currentPayload = payload;
            _onShow = onShow;
            _onCloseStarted = onCloseStarted;
            _onClosed = onClosed;
            _pendingCloseReason = PopupCloseReason.Cancel;
            State = PopupFrameState.Showing;

            onBind(payload);
            ApplyTopState(_isTop);

            if (ShouldPlayShowTransition())
            {
                var handle = _transitionPlayer.Play(_showTransitionId, HandleShowComplete);
                if (handle == null || handle.IsCanceled)
                {
                    HandleShowComplete();
                }

                return;
            }

            HandleShowComplete();
        }

        internal void CloseFromManager(PopupCloseReason reason)
        {
            BeginClose(reason);
        }

        internal void SetTopState(bool isTop, bool allowInput)
        {
            _isTop = isTop;
            ApplyTopState(allowInput);
            onTopStateChanged(isTop, allowInput);
        }

        public void CloseCompleted()
        {
            ClosePopup(PopupCloseReason.Yes);
        }

        public void CloseCanceled()
        {
            ClosePopup(PopupCloseReason.No);
        }

        protected virtual void ClosePopup(PopupCloseReason reason = PopupCloseReason.Yes)
        {
            BeginClose(reason);
        }

        protected virtual void onBind(object payload) { }
        protected virtual void onTopStateChanged(bool isTop, bool allowInput) { }

        private void BeginClose(PopupCloseReason reason)
        {
            if (State == PopupFrameState.Closing)
            {
                return;
            }

            ResolveDefaults();
            _transitionPlayer.Cancel();
            _pendingCloseReason = reason;
            State = PopupFrameState.Closing;
            ApplyTopState(false);
            _onCloseStarted?.Invoke(this, reason);

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

        private void HandleShowComplete()
        {
            if (State != PopupFrameState.Showing)
            {
                return;
            }

            State = PopupFrameState.Show;
            _onShow?.Invoke(this);
        }

        private void CompleteClose()
        {
            if (State != PopupFrameState.Closing)
            {
                return;
            }

            var callback = _onClosed;
            callback?.Invoke(this, _pendingCloseReason);
        }

        private bool ShouldPlayShowTransition()
        {
            return PlayShowTransition
                && _showTransitionId != null
                && _showTransitionId.IsValid
                && _transitionPlayer != null;
        }

        private bool ShouldPlayCloseTransition()
        {
            return PlayCloseTransition
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

            _onShow = null;
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
