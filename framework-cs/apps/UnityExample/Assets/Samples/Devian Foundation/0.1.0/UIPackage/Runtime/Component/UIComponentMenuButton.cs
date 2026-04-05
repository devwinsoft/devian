using UnityEngine;
using UnityEngine.UI;

namespace Devian
{
    [RequireComponent(typeof(Button))]
    [RequireComponent(typeof(UITransitionPlayer))]
    [RequireComponent(typeof(LayoutElement))]
    public class UIComponentMenuButton : UIComponentBase
    {
        [SerializeField] private int _index;
        [SerializeField] private UI_TRANSITION_PRESET_ID _selectTransitionId;
        [SerializeField] private UI_TRANSITION_PRESET_ID _deselectTransitionId;

        public int index => _index;
        public bool isSelected { get; private set; }

        private Button _button;
        private UIComponentButton _componentButton;
        private RectTransform _rectTransform;
        private LayoutElement _layoutElement;
        private UIComponentMenuBar _ownerBar;
        private UITransitionPlayer _transitionPlayer;
        private UITweenHandle _transitionHandle;

        protected override void onAwake()
        {
            _button = GetComponent<Button>();
            _componentButton = GetComponent<UIComponentButton>();
            _rectTransform = GetComponent<RectTransform>();
            _transitionPlayer = resolveTransitionPlayer();
            _layoutElement = resolveLayoutElement();
        }

        protected override void onInit(Canvas canvas)
        {
            _button ??= GetComponent<Button>();
            _componentButton ??= GetComponent<UIComponentButton>();
            _rectTransform ??= GetComponent<RectTransform>();
            _transitionPlayer ??= resolveTransitionPlayer();
            _layoutElement ??= resolveLayoutElement();
            _ownerBar = resolveOwnerBar();

            cancelTransition();
            if (isSelected) onSelect();
            else onDeselect();

            if (_componentButton != null)
                _componentButton.AddClickListener(onClickButton);
            else if (_button != null)
                _button.onClick.AddListener(onClickButton);
        }

        protected override void onPoolDespawned()
        {
            _SetSelected(false, animated: false);
            removeClickListener();
            _ownerBar = null;
        }

        protected override void onDestroy()
        {
            _SetSelected(false, animated: false);
            removeClickListener();
        }

        internal void _SetSelected(bool selected, bool animated)
        {
            cancelTransition();

            if (isSelected == selected)
                return;

            isSelected = selected;

            if (selected)
                onSelect();
            else
                onDeselect();

            if (!animated)
                return;

            var presetId = selected ? _selectTransitionId : _deselectTransitionId;
            if (presetId == null || !presetId.IsValid)
                return;

            _transitionPlayer ??= resolveTransitionPlayer();
            if (_transitionPlayer == null)
                return;

            var handle = _transitionPlayer.Play(presetId);
            if (handle == null || handle.IsCanceled)
            {
                _transitionHandle = null;
                return;
            }

            _transitionHandle = handle;
        }

        protected virtual void onSelect()
        {
        }

        protected virtual void onDeselect()
        {
        }

        private void onClickButton()
        {
            _ownerBar ??= resolveOwnerBar();
            if (_ownerBar == null)
            {
                Debug.LogWarning(
                    $"[{GetType().Name}] Parent UIComponentMenuBar was not found on direct parent.",
                    this);
                return;
            }

            _ownerBar.Select(_index);
        }

        private UIComponentMenuBar resolveOwnerBar()
        {
            return transform.parent == null
                ? null
                : transform.parent.GetComponent<UIComponentMenuBar>();
        }

        private UITransitionPlayer resolveTransitionPlayer()
        {
            var player = GetComponent<UITransitionPlayer>();
            if (player != null)
                return player;

            return gameObject.AddComponent<UITransitionPlayer>();
        }

        private LayoutElement resolveLayoutElement()
        {
            var element = GetComponent<LayoutElement>();
            if (element == null)
                element = gameObject.AddComponent<LayoutElement>();

            if (_rectTransform != null)
            {
                if (element.preferredWidth < 0f)
                    element.preferredWidth = _rectTransform.sizeDelta.x;

                if (element.preferredHeight < 0f)
                    element.preferredHeight = _rectTransform.sizeDelta.y;
            }

            return element;
        }

        private void removeClickListener()
        {
            if (_componentButton != null)
                _componentButton.RemoveClickListener(onClickButton);
            else if (_button != null)
                _button.onClick.RemoveListener(onClickButton);
        }

        private void cancelTransition()
        {
            if (_transitionHandle != null)
            {
                _transitionHandle.Cancel();
                _transitionHandle = null;
            }

            if (_transitionPlayer != null)
                _transitionPlayer.Cancel();
        }
    }
}
