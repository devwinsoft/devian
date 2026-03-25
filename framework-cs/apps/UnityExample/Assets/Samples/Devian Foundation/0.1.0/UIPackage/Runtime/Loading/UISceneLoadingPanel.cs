using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Devian
{
    public sealed class UISceneLoadingPanel : UIBasePanel<UILoadingCanvas>
    {
        [SerializeField] private TextMeshProUGUI _progressText;
        [SerializeField] private Image _progressFill;

        private float _progress;

        protected override void onAwake()
        {
        }

        protected override void onInit(UILoadingCanvas canvas)
        {
            SetProgress(0f);
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

        public void SetProgress(float progress)
        {
            _progress = Mathf.Clamp01(progress);

            if (_progressText != null)
            {
                _progressText.text = $"{Mathf.RoundToInt(_progress * 100f)}%";
            }
        }
    }
}
