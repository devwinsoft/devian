// SSOT: skills/devian-unity/10-foundation/17-scene-trans-manager/SKILL.md

#nullable enable

using System.Collections;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// 씬 라이프사이클 훅을 제공하는 추상 베이스 클래스.
    /// 씬 루트(또는 유일한 오브젝트)에 1개만 존재하도록 권장된다.
    /// SceneTransManager가 OnEnter/OnExit를 호출한다.
    /// OnStart는 SceneBase.Start()에서 호출된다.
    /// </summary>
    public abstract class SceneBase : MonoBehaviour
    {
        /// <summary>
        /// 씬 로드 시 Unity Awake()에서 항상 1회 호출되는 초기화 훅.
        /// 레퍼런스 캐싱, 초기 상태 구성, 컴포넌트 연결 등 전환과 무관한 준비 작업에 사용한다.
        /// </summary>
        protected virtual void OnInitAwake() { }

        protected virtual void Awake()
        {
            OnInitAwake();
        }

        /// <summary>
        /// Unity Start 코루틴. OnStart()를 호출한다.
        /// </summary>
        private IEnumerator Start()
        {
            yield return OnStart();
        }

        /// <summary>
        /// 씬 진입 시 호출되는 초기화 코루틴.
        /// SceneTransManager가 씬 로드 완료 후 또는 부팅 시 호출한다.
        /// </summary>
        public abstract IEnumerator OnEnter();

        /// <summary>
        /// 씬 시작 시 호출되는 코루틴.
        /// SceneBase.Start()에서 호출된다.
        /// </summary>
        public virtual IEnumerator OnStart() { yield break; }

        /// <summary>
        /// 씬 퇴장 시 호출되는 정리 코루틴.
        /// SceneTransManager가 새 씬 로드 전에 호출한다.
        /// </summary>
        public abstract IEnumerator OnExit();
    }
}
