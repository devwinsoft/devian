using UnityEngine;
using UnityEngine.UI;

namespace Devian
{
    public sealed class UILoadingCanvas : UIBaseCanvas<UILoadingCanvas>
    {
        [SerializeField] private UISpinnerLoadingPanel _spinnerPanel;
        [SerializeField] private UIBundleLoadingPanel _bundleLoadingPanel;
        [SerializeField] private UISceneLoadingPanel _sceneLoadingPanel;
        [SerializeField] private int _sortingOrder = 3;

        private int _spinnerCount;
        private int _bundleCount;
        private int _sceneCount;

        protected override void onAwake()
        {
            normalizeCanvasRect();
            resolveReferences();

            if (canvas != null)
            {
                canvas.sortingOrder = _sortingOrder;
            }
        }

        protected override void onInit()
        {
            Refresh();
        }

        public override bool Validate(out string reason)
        {
            if (!base.Validate(out reason))
            {
                return false;
            }

            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
            {
                reason = "UILoadingCanvas should not use WorldSpace render mode";
                return false;
            }

            if (_spinnerPanel == null)
            {
                reason = "UISpinnerLoadingPanel not found";
                return false;
            }

            if (_bundleLoadingPanel == null)
            {
                reason = "UIBundleLoadingPanel not found";
                return false;
            }

            if (_sceneLoadingPanel == null)
            {
                reason = "UISceneLoadingPanel not found";
                return false;
            }

            reason = null;
            return true;
        }

        public void ShowSpinner()
        {
            _spinnerCount++;
            Refresh();
        }

        public void HideSpinner()
        {
            _spinnerCount = Mathf.Max(0, _spinnerCount - 1);
            Refresh();
        }

        public void ShowBundleLoading()
        {
            if (_bundleCount == 0)
            {
                _bundleLoadingPanel?.SetProgress(0f);
            }

            _bundleCount++;
            Refresh();
        }

        public void HideBundleLoading()
        {
            _bundleCount = Mathf.Max(0, _bundleCount - 1);
            if (_bundleCount == 0)
            {
                _bundleLoadingPanel?.SetProgress(0f);
            }

            Refresh();
        }

        public void SetBundleLoadingProgress(float progress)
        {
            _bundleLoadingPanel?.SetProgress(progress);
        }

        public void SetSceneLoadingProgress(float progress)
        {
            _sceneLoadingPanel?.SetProgress(progress);
        }

        public void ShowSceneLoading()
        {
            _sceneCount++;
            Refresh();
        }

        public void HideSceneLoading()
        {
            _sceneCount = Mathf.Max(0, _sceneCount - 1);
            Refresh();
        }

        public void Refresh()
        {
            if (_sceneLoadingPanel != null)
            {
                if (_sceneCount > 0) _sceneLoadingPanel.Show();
                else _sceneLoadingPanel.Hide();
            }

            if (_bundleLoadingPanel != null)
            {
                if (_sceneCount <= 0 && _bundleCount > 0) _bundleLoadingPanel.Show();
                else _bundleLoadingPanel.Hide();
            }

            if (_spinnerPanel != null)
            {
                if (_sceneCount <= 0 && _bundleCount <= 0 && _spinnerCount > 0) _spinnerPanel.Show();
                else _spinnerPanel.Hide();
            }
        }

        private void resolveReferences()
        {
            if (_spinnerPanel == null)
            {
                _spinnerPanel = GetComponentInChildren<UISpinnerLoadingPanel>(true);
            }

            if (_bundleLoadingPanel == null)
            {
                _bundleLoadingPanel = GetComponentInChildren<UIBundleLoadingPanel>(true);
            }

            if (_sceneLoadingPanel == null)
            {
                _sceneLoadingPanel = GetComponentInChildren<UISceneLoadingPanel>(true);
            }
        }

        private void normalizeCanvasRect()
        {
            var rect = transform as RectTransform;
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }
}
