namespace Devian
{
    /// <summary>
    /// 오브젝트 부착형 입력 소비 컨트롤러 계약.
    /// </summary>
    public interface IBaseInputController
    {
        /// <summary>
        /// 입력 수신 활성화 여부.
        /// </summary>
        bool InputEnabled { get; set; }

        /// <summary>
        /// 우선순위. 값이 높을수록 먼저 호출된다.
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Move 입력 변환 전략.
        /// </summary>
        IInputSpace InputSpace { get; set; }
    }
}
