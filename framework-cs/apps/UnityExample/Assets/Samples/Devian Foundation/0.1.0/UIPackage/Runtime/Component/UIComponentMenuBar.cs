using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Devian
{
    [RequireComponent(typeof(HorizontalLayoutGroup))]
    public class UIComponentMenuBar : UIComponentBase
    {
        [SerializeField] private int _initialSelectedIndex = -1;

        private readonly List<UIComponentMenuButton> _buttons = new List<UIComponentMenuButton>();
        private readonly Dictionary<int, UIComponentMenuButton> _buttonsByIndex =
            new Dictionary<int, UIComponentMenuButton>();

        public int selectedIndex { get; private set; } = -1;
        public UIComponentMenuButton selectedButton { get; private set; }

        public event Action<int> OnSelect;

        protected override void onInit(Canvas canvas)
        {
            rebuildButtons();

            if (_initialSelectedIndex >= 0
                && _buttonsByIndex.TryGetValue(_initialSelectedIndex, out var initialButton))
            {
                selectedIndex = _initialSelectedIndex;
                selectedButton = initialButton;
                selectedButton._SetSelected(true, animated: false);
            }
        }

        protected override void onPoolDespawned()
        {
            resetSelectionState();
            clearButtons();
            OnSelect = null;
        }

        protected override void onDestroy()
        {
            resetSelectionState();
            clearButtons();
            OnSelect = null;
        }

        public void Select(int index)
        {
            if (!_buttonsByIndex.TryGetValue(index, out var targetButton))
            {
                Debug.LogWarning(
                    $"[{GetType().Name}] Select: button with index '{index}' was not found.",
                    this);
                return;
            }

            if (selectedIndex == index && selectedButton == targetButton)
                return;

            selectedButton?._SetSelected(false, animated: true);

            selectedIndex = index;
            selectedButton = targetButton;
            selectedButton._SetSelected(true, animated: true);
            OnSelect?.Invoke(index);
        }

        private void rebuildButtons()
        {
            clearButtons();
            resetSelectionState();

            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                var button = child.GetComponent<UIComponentMenuButton>();
                if (button == null)
                    continue;

                _buttons.Add(button);
                if (_buttonsByIndex.ContainsKey(button.index))
                {
                    Debug.LogError(
                        $"[{GetType().Name}] Duplicate menu button index '{button.index}' on child '{child.name}'.",
                        this);
                    continue;
                }

                _buttonsByIndex.Add(button.index, button);
                button._SetSelected(false, animated: false);
            }
        }

        private void clearButtons()
        {
            _buttons.Clear();
            _buttonsByIndex.Clear();
        }

        private void resetSelectionState()
        {
            selectedButton?._SetSelected(false, animated: false);
            selectedButton = null;
            selectedIndex = -1;
        }
    }
}
