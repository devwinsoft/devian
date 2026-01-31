// SSOT: skills/devian-unity/30-unity-components/27-bootstrap-resource-object/SKILL.md

#nullable enable

using System.Collections;

namespace Devian
{
    /// <summary>
    /// 부팅 단계(스텝) 인터페이스.
    /// BootCoordinator는 Order 오름차순으로 모든 스텝을 실행한다.
    /// </summary>
    public interface IDevianBootStep
    {
        int Order { get; }

        /// <summary>
        /// 부팅 단계 실행 코루틴. 실패 시 예외를 던져 부팅 실패로 간주한다.
        /// </summary>
        IEnumerator Boot();
    }
}
