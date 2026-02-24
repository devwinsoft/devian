// SSOT: skills/devian-unity/10-foundation/17-scene-trans-manager/SKILL.md

#nullable enable

using System.Collections;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// SceneBase에 Bootstrap 통합 로직을 추가한 클래스.
    /// 대부분의 게임 씬은 이 클래스를 상속한다.
    ///
    /// Awake()에서 Bootstrap 생성을 트리거하고,
    /// Start()에서 BootProc 완료를 보장한 뒤 OnStart()를 호출한다.
    /// </summary>
    public abstract class SceneBoot : SceneBase
    {
        protected override void Awake()
        {
            base.Awake();

            // Bootstrap이 아직 생성되지 않았으면 생성 트리거
            if (!BaseBootstrap.IsCreated)
            {
                BaseBootstrap.CreateFromResources();
            }
        }

        /// <summary>
        /// Unity Start 코루틴. BootProc 완료 후 OnStart()를 호출한다.
        /// </summary>
        private new IEnumerator Start()
        {
            yield return BaseBootstrap.BootProc();
            yield return OnStart();
        }
    }
}
