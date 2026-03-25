using UnityEngine;
using UnityEngine.UI;

namespace Devian
{
    public sealed class UISpinnerLoadingPanel : UIBasePanel<UILoadingCanvas>
    {
        private const int SpinnerBarCount = 12;
        private const float SpinnerRadius = 34f;

        [SerializeField] private Image _dim;
        [SerializeField] private RectTransform _spinnerRoot;
        [SerializeField] private float _rotationSpeed = 180f;

        protected override void onAwake()
        {
            resolveReferences();
        }

        protected override void onInit(UILoadingCanvas canvas)
        {
            ensureLayout();
            Hide();
        }

        protected override void onShow()
        {
            gameObject.SetActive(true);
        }

        protected override void onHide()
        {
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!isShown || _spinnerRoot == null)
            {
                return;
            }

            _spinnerRoot.Rotate(0f, 0f, -_rotationSpeed * Time.unscaledDeltaTime);
        }

        private void ensureLayout()
        {
            var panelRect = rectTransform;
            UILoadingUIUtil.StretchFull(panelRect);

            if (_dim == null)
            {
                var dimRect = UILoadingUIUtil.CreateUIObject("Dim", transform);
                UILoadingUIUtil.StretchFull(dimRect);
                var dim = UILoadingUIUtil.GetOrAddImage(dimRect.gameObject);
                dim.color = new Color(0f, 0f, 0f, 0.52f);
                dim.raycastTarget = true;
                _dim = dim;
            }

            if (_spinnerRoot == null)
            {
                _spinnerRoot = UILoadingUIUtil.CreateUIObject("SpinnerRoot", transform);
            }

            UILoadingUIUtil.CenterRect(_spinnerRoot, new Vector2(96f, 96f));

            if (_spinnerRoot.childCount > 0)
            {
                return;
            }

            for (var i = 0; i < SpinnerBarCount; i++)
            {
                var barRect = UILoadingUIUtil.CreateUIObject($"Bar{i:00}", _spinnerRoot);
                UILoadingUIUtil.EnsureCanvasRenderer(barRect.gameObject);

                var image = UILoadingUIUtil.GetOrAddImage(barRect.gameObject);
                image.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.18f, 1f, i / (float)(SpinnerBarCount - 1)));
                image.raycastTarget = false;

                barRect.anchorMin = new Vector2(0.5f, 0.5f);
                barRect.anchorMax = new Vector2(0.5f, 0.5f);
                barRect.pivot = new Vector2(0.5f, 0.5f);
                barRect.sizeDelta = new Vector2(10f, 24f);
                barRect.localScale = Vector3.one;

                var angle = i * (360f / SpinnerBarCount);
                var radians = angle * Mathf.Deg2Rad;
                barRect.anchoredPosition = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians)) * SpinnerRadius;
                barRect.localRotation = Quaternion.Euler(0f, 0f, -angle);
            }
        }

        private void resolveReferences()
        {
            if (_dim == null)
            {
                _dim = GetComponentInChildren<Image>(true);
            }

            if (_spinnerRoot == null)
            {
                var child = transform.Find("SpinnerRoot");
                if (child != null)
                {
                    _spinnerRoot = child as RectTransform;
                }
            }
        }
    }
}
