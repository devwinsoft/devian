// SSOT: skills/devian-unity/30-unity-components/15-scene-trans-manager/SKILL.md

#nullable enable

using System.Collections;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// 씬 루트(또는 유일한 오브젝트)에 1개만 존재하도록 권장되는 씬 라이프사이클 훅.
    /// SceneTransManager가 전환 시 OnExit → Load → OnEnter 순으로 호출한다.
    /// </summary>
    public abstract class BaseScene : MonoBehaviour
    {
        /// <summary>
        /// OnEnter()가 이미 호출되었는지 여부.
        /// 중복 호출 방지에 사용된다.
        /// </summary>
        internal bool HasEntered { get; private set; }

        /// <summary>
        /// 씬 로드 시 Unity Awake()에서 항상 1회 호출되는 초기화 훅.
        /// 레퍼런스 캐싱, 초기 상태 구성, 컴포넌트 연결 등 전환과 무관한 준비 작업에 사용한다.
        /// </summary>
        protected virtual void OnInitAwake() { }

        private void Awake()
        {
            OnInitAwake();
        }

        /// <summary>
        /// OnEnter()가 호출되었음을 표시한다.
        /// SceneTransManager 내부에서만 호출해야 한다.
        /// </summary>
        internal void _MarkEntered()
        {
            HasEntered = true;
        }

        /// <summary>
        /// 씬 진입 시 호출되는 초기화 코루틴.
        /// SceneTransManager가 씬 로드 완료 후 또는 부팅 시 1회 호출한다.
        /// 한 씬 인스턴스 당 1회만 실행된다 (중복 방지).
        /// </summary>
        public abstract IEnumerator OnEnter();

        /// <summary>
        /// 씬 퇴장 시 호출되는 정리 코루틴.
        /// SceneTransManager가 새 씬 로드 전에 호출한다.
        /// </summary>
        public abstract IEnumerator OnExit();
    }
}
