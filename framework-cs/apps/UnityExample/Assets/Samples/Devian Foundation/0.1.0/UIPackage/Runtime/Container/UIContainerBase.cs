using UnityEngine;

namespace Devian
{
    /// <summary>
    /// UICanvas/UICanvasFrame 수명주기에 통합되는 컨테이너의 기반 클래스.
    /// UICanvas.Init() 시 자동으로 수집되어 초기화된다.
    /// 초기화 순서: Container.onInit() → Frame.onInit() → Canvas.onInit()
    /// </summary>
    public abstract class UIContainerBase : MonoBehaviour
    {
        /// <summary>
        /// 컨테이너가 초기화되었는지 여부.
        /// </summary>
        public bool isContainerInitialized { get; private set; }

        /// <summary>
        /// 소유 Canvas. _Init(canvas) 호출 시 설정된다.
        /// </summary>
        public Canvas canvas { get; private set; }

        /// <summary>
        /// 컨테이너 초기화. UICanvas.Init()에서 호출된다.
        /// Canvas 참조를 저장하고 onInit()을 호출한다.
        /// 중복 호출 방지 (isContainerInitialized 가드).
        /// </summary>
        internal void _Init(Canvas canvas)
        {
            if (isContainerInitialized) return;
            this.canvas = canvas;
            isContainerInitialized = true;
            onInit();
        }

        /// <summary>
        /// 모든 초기화가 완료된 후 호출.
        /// Container, Frame, Canvas의 Init이 모두 끝난 뒤 호출된다.
        /// </summary>
        internal void _InitComplete()
        {
            onInitComplete();
        }

        /// <summary>
        /// 컨테이너 초기화 로직. override하여 사용.
        /// 호출 시점: Frame.onInit(), Canvas.onInit() 이전.
        /// </summary>
        protected virtual void onInit() { }

        /// <summary>
        /// 모든 초기화가 완료된 후. override하여 사용.
        /// 호출 시점: Canvas.onInitComplete() 이전.
        /// </summary>
        protected virtual void onInitComplete() { }
    }
}
