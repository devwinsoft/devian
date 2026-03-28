using UnityEngine;
using UnityEngine.UI;

namespace Devian
{
    [RequireComponent(typeof(Image))]
    public sealed class UIComponentRedDot : UIComponentBase
    {
        [SerializeField] private string _redDotKey;

        private Image _image;

        protected override void onAwake()
        {
            _image = GetComponent<Image>();
        }

        protected override void onInit(Canvas canvas)
        {
            var key = getRedDotKey();
            if (string.IsNullOrEmpty(key))
            {
                setImageEnabled(false);
                return;
            }

            var manager = RedDotManager.Instance;
            setImageEnabled(manager.IsOn(key));
            manager.Subcribe(GetEntityId(), key, onRedDotChanged);
        }

        protected override void onDestroy()
        {
            RedDotManager.Instance.UnSubcribe(GetEntityId());
        }

        private void onRedDotChanged(RedDotChanged changed)
        {
            setImageEnabled(changed.IsOn);
        }

        private string getRedDotKey()
        {
            return string.IsNullOrWhiteSpace(_redDotKey)
                ? null
                : _redDotKey.Trim();
        }

        private void setImageEnabled(bool enabled)
        {
            if (_image != null)
                _image.enabled = enabled;
        }
    }
}
