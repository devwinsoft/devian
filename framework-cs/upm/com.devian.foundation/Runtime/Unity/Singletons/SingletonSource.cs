// SSOT: skills/devian-unity/10-base-system/31-singleton/SKILL.md

namespace Devian
{
    /// <summary>
    /// 싱글톤 등록 소스. 우선순위: Compo > Boot > Auto
    /// </summary>
    public enum SingletonSource
    {
        /// <summary>
        /// 자동 생성 (최저 우선순위)
        /// </summary>
        Auto = 0,

        /// <summary>
        /// Bootstrap에서 등록 (중간 우선순위)
        /// </summary>
        Boot = 1,

        /// <summary>
        /// 씬/프리팹 컴포넌트 (최고 우선순위)
        /// </summary>
        Compo = 2,
    }
}
