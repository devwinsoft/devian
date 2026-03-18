using UnityEngine;

namespace Devian
{
    /// <summary>
    /// UICanvas/UIFrame 수명주기에 통합되는 컴포넌트의 기반 클래스.
    /// UICanvas.Init() 시 자동으로 수집되어 초기화된다.
    /// 초기화 순서: Component.onInit() → Frame.onInit() → Canvas.onInit()
    /// </summary>
    public abstract class UIComponentBase : MonoBehaviour
    {
        /// <summary>
        /// 컴포넌트가 초기화되었는지 여부.
        /// </summary>
        public bool isComponentInitialized { get; private set; }

        /// <summary>
        /// 컴포넌트 초기화. UICanvas.Init()에서 호출된다.
        /// 중복 호출 방지 (isComponentInitialized 가드).
        /// </summary>
        internal void _Init()
        {
            if (isComponentInitialized) return;
            isComponentInitialized = true;
            onInit();
        }

        /// <summary>
        /// 모든 초기화가 완료된 후 호출.
        /// Component, Frame, Canvas의 Init이 모두 끝난 뒤 호출된다.
        /// </summary>
        internal void _InitComplete()
        {
            onInitComplete();
        }

        /// <summary>
        /// 컴포넌트 초기화 로직. override하여 사용.
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
