using UnityEngine;

namespace Devian
{
    /// <summary>
    /// UIBaseContainer 내부의 하위 요소(Frame/Grid) 기반 클래스.
    /// UIBaseContainer가 onInitComplete() 시 수집하여 초기화한다.
    /// Scroll 정보를 소유하지 않는다.
    /// Scroll 전용 계약은 IUIScrollSection 인터페이스로 분리된다.
    /// </summary>
    public abstract class UIBaseFrame : MonoBehaviour
    {
        // ─── Lifecycle ───

        public bool isFrameInitialized { get; private set; }
        public Canvas canvas { get; private set; }

        /// <summary>캐시된 RectTransform.</summary>
        public RectTransform rectTransform { get; private set; }

        protected void Awake()
        {
            rectTransform = (RectTransform)transform;
            onAwake();
        }

        /// <summary>Override this for custom Awake logic.</summary>
        protected virtual void onAwake() { }

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

        public virtual float GetWidth() => UIUtils.GetWidth(rectTransform);
        public virtual float GetHeight() => UIUtils.GetHeight(rectTransform);

        // ─── Clear ───

        internal virtual void _Clear() { }
    }
}
