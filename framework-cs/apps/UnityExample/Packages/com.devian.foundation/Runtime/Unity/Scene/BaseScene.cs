// SSOT: skills/devian-unity/30-unity-components/15-scene-trans-manager/SKILL.md

#nullable enable

using System.Collections;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// 씬 루트(또는 유일한 오브젝트)에 1개만 존재하도록 권장되는 씬 라이프사이클 훅.
    /// SceneTransManager가 OnEnter/OnExit를 호출한다.
    /// OnStart는 BaseScene.Start()에서 호출된다.
    /// </summary>
    public abstract class BaseScene : MonoBehaviour
    {
        /// <summary>
        /// Bootstrap 사용 여부. 기본 true.
        /// false로 override하면 해당 씬에서는 부트 트리거를 스킵한다.
        /// </summary>
        protected virtual bool UseBootstrap => true;

        /// <summary>
        /// 씬 로드 시 Unity Awake()에서 항상 1회 호출되는 초기화 훅.
        /// 레퍼런스 캐싱, 초기 상태 구성, 컴포넌트 연결 등 전환과 무관한 준비 작업에 사용한다.
        /// </summary>
        protected virtual void OnInitAwake() { }

        private void Awake()
        {
            OnInitAwake();

            // UseBootstrap이고 Bootstrap이 아직 생성되지 않았으면 생성 트리거
            if (UseBootstrap && !BaseBootstrap.IsCreated)
            {
                BaseBootstrap.CreateFromResources();
            }
        }

        /// <summary>
        /// Unity Start 코루틴. OnStart()를 호출한다.
        /// </summary>
        private IEnumerator Start()
        {
            // BootProc 호출 (이미 부팅이면 즉시 종료)
            if (UseBootstrap)
            {
                yield return BaseBootstrap.BootProc();
            }

            yield return OnStart();
        }

        /// <summary>
        /// 씬 진입 시 호출되는 초기화 코루틴.
        /// SceneTransManager가 씬 로드 완료 후 또는 부팅 시 호출한다.
        /// </summary>
        public abstract IEnumerator OnEnter();

        /// <summary>
        /// 씬 시작 시 호출되는 코루틴.
        /// BaseScene.Start()에서 호출된다.
        /// </summary>
        public virtual IEnumerator OnStart() { yield break; }

        /// <summary>
        /// 씬 퇴장 시 호출되는 정리 코루틴.
        /// SceneTransManager가 새 씬 로드 전에 호출한다.
        /// </summary>
        public abstract IEnumerator OnExit();
    }
}
