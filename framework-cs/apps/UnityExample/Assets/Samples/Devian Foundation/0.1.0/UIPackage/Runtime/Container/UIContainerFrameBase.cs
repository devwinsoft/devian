using UnityEngine;

namespace Devian
{
    /// <summary>
    /// UIContainerBase 내부의 하위 요소(Frame/Grid) 기반 클래스.
    /// UIContainerBase가 onInitComplete() 시 수집하여 초기화한다.
    /// Scroll 정보를 소유하지 않는다.
    /// Scroll 전용 계약은 IScrollSection 인터페이스로 분리된다.
    /// </summary>
    public abstract class UIContainerFrameBase : MonoBehaviour
    {
        // ─── Lifecycle ───

        public bool isFrameInitialized { get; private set; }
        public Canvas canvas { get; private set; }

        internal void _Init(Canvas canvas)
        {
            if (isFrameInitialized) return;
            this.canvas = canvas;
            isFrameInitialized = true;
            onInit();
        }

        internal void _InitComplete() { onInitComplete(); }

        protected virtual void onInit() { }
        protected virtual void onInitComplete() { }

        // ─── Size ───

        public virtual float GetWidth() => UIUtils.GetWidth((RectTransform)transform);
        public virtual float GetHeight() => UIUtils.GetHeight((RectTransform)transform);

        // ─── Clear ───

        internal virtual void _Clear() { }
    }
}
