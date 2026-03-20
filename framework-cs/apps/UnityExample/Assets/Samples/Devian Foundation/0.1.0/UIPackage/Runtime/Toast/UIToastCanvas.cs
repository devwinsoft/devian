using UnityEngine;

namespace Devian
{
    public sealed class UIToastCanvas : UICanvas<UIToastCanvas>, IPoolable
    {
        [SerializeField] private UIToastPanel _panel;

        public UIToastPanel panel => _panel;

        protected override void onAwake()
        {
            NormalizeCanvasRect();

            if (_panel == null)
            {
                _panel = GetComponentInChildren<UIToastPanel>(true);
            }
        }

        public override bool Validate(out string reason)
        {
            if (!base.Validate(out reason))
            {
                return false;
            }

            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
            {
                reason = "UIToastCanvas should not use WorldSpace render mode";
                return false;
            }

            if (_panel == null)
            {
                reason = "UIToastPanel not found";
                return false;
            }

            reason = null;
            return true;
        }

        public void OnPoolSpawned()
        {
            NormalizeCanvasRect();

            if (_panel == null)
            {
                _panel = GetComponentInChildren<UIToastPanel>(true);
            }
        }

        public void OnPoolDespawned()
        {
        }

        private void NormalizeCanvasRect()
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
