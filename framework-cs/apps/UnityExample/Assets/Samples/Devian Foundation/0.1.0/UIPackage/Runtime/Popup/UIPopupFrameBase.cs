using System;
using UnityEngine;

namespace Devian
{
    [RequireComponent(typeof(UITransitionPlayer))]
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIPopupFrameBase : UIBaseFrame, IPoolable
    {
        [SerializeField] private UI_TRANSITION_PRESET_ID _openTransitionId;
        [SerializeField] private UI_TRANSITION_PRESET_ID _closeTransitionId;

        private UITransitionPlayer _transitionPlayer;
        private CanvasGroup _rootCanvasGroup;
        private Action<UIPopupFrameBase> _onOpened;
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
        protected virtual bool PlayOpenTransition => UIPopupDefaults.DefaultPlayOpenTransition;
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
            _pendingCloseReason = PopupCloseReason.Canceled;
            _isTop = false;
        }

        public void OnPoolDespawned()
        {
            CancelInternal();
            _currentPayload = null;
            _pendingCloseReason = PopupCloseReason.Canceled;
            _isTop = false;
        }

        internal void OpenUntyped(
            object payload,
            Action<UIPopupFrameBase> onOpened,
            Action<UIPopupFrameBase, PopupCloseReason> onCloseStarted,
            Action<UIPopupFrameBase, PopupCloseReason> onClosed)
        {
            ResolveDefaults();
            CancelInternal();

            _currentPayload = payload;
            _onOpened = onOpened;
            _onCloseStarted = onCloseStarted;
            _onClosed = onClosed;
            _pendingCloseReason = PopupCloseReason.Canceled;
            State = PopupFrameState.Opening;

            onBind(payload);
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
            ClosePopup(PopupCloseReason.Completed);
        }

        public void CloseCanceled()
        {
            ClosePopup(PopupCloseReason.Canceled);
        }

        protected virtual void ClosePopup(PopupCloseReason reason = PopupCloseReason.Completed)
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

        private void HandleOpenCompleted()
        {
            if (State != PopupFrameState.Opening)
            {
                return;
            }

            State = PopupFrameState.Opened;
            _onOpened?.Invoke(this);
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

        private bool ShouldPlayOpenTransition()
        {
            return PlayOpenTransition
                && _openTransitionId != null
                && _openTransitionId.IsValid
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
