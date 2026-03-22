using UnityEngine;
namespace Devian
{
    public sealed class UIPopupPanel : UIPanel<UIPopupCanvas>
    {
        [SerializeField] private UIPopupDim _dim;
        [SerializeField] private RectTransform _popupRoot;

        public RectTransform popupRoot => _popupRoot != null ? _popupRoot : rectTransform;

        protected override void onAwake()
        {
            ResolveDefaults();
        }

        protected override void onInit(UIPopupCanvas canvas)
        {
            ResolveDefaults();
        }

        protected override void onInitComplete()
        {
            ResolveDefaults();
            ApplyModalState(false, false, false, UIPopupDefaults.DefaultDimColor, 0f);
        }

        public void AttachFrame(UIPopupFrame frame)
        {
            if (frame == null)
            {
                return;
            }

            ResolveDefaults();

            frame.transform.SetParent(popupRoot, false);
            frame.transform.SetAsLastSibling();

            if (!frame.isFrameInitialized && owner != null && owner.canvas != null)
            {
                frame._Init(owner.canvas);
                frame._InitComplete();
            }

            EnsureLayerOrder();
        }

        public void DetachFrame(UIPopupFrame frame)
        {
            if (frame == null)
            {
                return;
            }

            if (frame.transform.parent == popupRoot)
            {
                frame.transform.SetParent(null, false);
            }
        }

        public void ApplyModalState(bool showDim, bool blockBehind, bool closeOnDimClick, Color dimColor, float dimAlpha)
        {
            ResolveDefaults();
            EnsureLayerOrder();
            _dim?.ApplyState(showDim, blockBehind, closeOnDimClick, dimColor, dimAlpha);
        }

        private void ResolveDefaults()
        {
            if (_popupRoot == null)
            {
                var popupRootTransform = transform.Find("PopupRoot");
                if (popupRootTransform != null)
                {
                    _popupRoot = popupRootTransform as RectTransform;
                }
            }

            if (_dim == null)
            {
                var dimTransform = transform.Find("Dim");
                if (dimTransform != null)
                {
                    _dim = dimTransform.GetComponent<UIPopupDim>();
                }
            }

            if (_popupRoot == null)
            {
                _popupRoot = rectTransform;
            }
        }

        private void EnsureLayerOrder()
        {
            if (_dim != null)
            {
                _dim.transform.SetAsFirstSibling();
            }

            if (_popupRoot != null)
            {
                _popupRoot.SetAsLastSibling();
            }
        }
    }
}
