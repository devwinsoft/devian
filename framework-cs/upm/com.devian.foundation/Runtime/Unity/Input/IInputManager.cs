using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace Devian
{
    /// <summary>
    /// InputActionAsset 기반 입력 수집/정규화/발행 관리자 계약.
    /// </summary>
    public interface IInputManager
    {
        /// <summary>
        /// 사용 중인 InputActionAsset.
        /// </summary>
        InputActionAsset Asset { get; }

        /// <summary>
        /// 현재 입력 컨텍스트.
        /// </summary>
        InputContext Context { get; }

        /// <summary>
        /// key("Map/Action") → button index 맵 조회.
        /// 없으면 -1 반환.
        /// </summary>
        int GetButtonIndex(string key);

        /// <summary>
        /// index → key("Map/Action") 역매핑.
        /// ButtonKeys[i]는 button index i에 해당하는 action key.
        /// </summary>
        IReadOnlyList<string> ButtonKeys { get; }

        /// <summary>
        /// 입력 컨텍스트를 전환한다.
        /// </summary>
        void SetContext(InputContext context);

        /// <summary>
        /// _expectedButtonKeys 기준으로 내부 버튼 맵을 재빌드한다.
        /// Edit 모드에서도 안전하게 호출 가능.
        /// </summary>
        void RebuildButtonMap();
    }
}
