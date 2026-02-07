using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Move 입력(Vector2)을 월드 공간(Vector3)으로 변환하는 전략 인터페이스.
    /// </summary>
    public interface IInputSpace
    {
        /// <summary>
        /// raw Move(Vector2)를 월드 방향 Vector3로 변환한다.
        /// </summary>
        Vector3 ResolveMove(Vector2 raw);
    }
}
