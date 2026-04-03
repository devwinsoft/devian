using UnityEngine;

namespace Devian
{
    public enum UIPageState
    {
        Hidden,
        Entering,
        Visible,
        Exiting
    }

    public enum UIPageTransitionDirection
    {
        None,
        Left,
        Right
    }

    public enum UIPageSubCloseReason
    {
        Normal,
        MainSwitch,
        ReplacedByAnotherSub,
        OwnerCanvasDespawn
    }

    public enum UIPagePopupCloseReason
    {
        Normal,
        MainSwitch,
        ReplacedByAnotherPopup,
        OwnerCanvasDespawn
    }

    public abstract class UIBasePagePanel : UIBasePanel
    {
        [SerializeField] private int _pageIndex;

        public int pageIndex => _pageIndex;
        public UIBasePageCanvas ownerCanvas => ownerBase as UIBasePageCanvas;

        protected sealed override void onInitFromCanvas(MonoBehaviour canvasOwner)
        {
            var pageCanvas = canvasOwner as UIBasePageCanvas;
            if (pageCanvas == null)
            {
                Debug.LogError(
                    $"[{GetType().Name}] Page owner must be UIBasePageCanvas. " +
                    $"Actual type: {canvasOwner?.GetType().Name ?? "null"}",
                    this);
                return;
            }

            onInit(pageCanvas);
        }

        protected virtual void onInit(UIBasePageCanvas canvas) { }
    }

    public abstract class UIBasePagePanel<TCanvas> : UIBasePagePanel
        where TCanvas : UIBasePageCanvas
    {
        protected new TCanvas ownerCanvas { get; private set; }

        protected sealed override void onInit(UIBasePageCanvas canvas)
        {
            ownerCanvas = canvas as TCanvas;
            if (ownerCanvas == null)
            {
                Debug.LogError(
                    $"[{GetType().Name}] Page canvas must be {typeof(TCanvas).Name}. " +
                    $"Actual type: {canvas?.GetType().Name ?? "null"}",
                    this);
                return;
            }

            onInit(ownerCanvas);
        }

        protected virtual void onInit(TCanvas canvas) { }
    }
}
