using UnityEngine;

namespace Devian
{
    /// <summary>
    /// UIBaseCanvas lifecycle에 통합되는 UI 컴포넌트 기반 클래스.
    /// component > frame > container 순서로 초기화되도록 설계한다.
    /// </summary>
    public abstract class UIComponentBase : MonoBehaviour
    {
        /// <summary>컴포넌트가 초기화되었는지 여부.</summary>
        public bool isInitialized { get; private set; }

        /// <summary>초기화에 사용된 Unity Canvas.</summary>
        public Canvas canvas { get; private set; }

        protected void Awake()
        {
            onAwake();

            var ownerCanvas = GetComponentInParent<UIBaseCanvas>();
            if (ownerCanvas != null && ownerCanvas.isInitialized)
            {
                Init(ownerCanvas.canvas);
            }
        }

        protected void OnDestroy()
        {
            if (Application.isPlaying
                && !BaseApplication.IsShuttingDown
                && !BaseApplication.IsApplicationQuitting)
            {
                onDestroy();
            }
        }

        public void Init(Canvas canvas)
        {
            if (isInitialized) return;

            this.canvas = canvas;
            isInitialized = true;
            onInit(canvas);
        }

        internal void _ResetForPool()
        {
            if (!isInitialized && canvas == null)
                return;

            onPoolDespawned();
            isInitialized = false;
            canvas = null;
        }

        protected virtual void onAwake() { }
        protected virtual void onInit(Canvas canvas) { }
        protected virtual void onPoolDespawned() { }
        protected virtual void onDestroy() { }
    }
}
