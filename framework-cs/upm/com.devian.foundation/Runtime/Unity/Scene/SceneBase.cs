// SSOT: skills/devian-unity/10-foundation/17-scene-trans-manager/SKILL.md

#nullable enable

using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// 씬 라이프사이클 훅을 제공하는 추상 베이스 클래스.
    /// 씬 루트(또는 유일한 오브젝트)에 1개만 존재하도록 권장된다.
    /// SceneTransManager가 Enter/Exit를 호출한다.
    /// onStart는 SceneBase.Start()에서 호출된다.
    /// </summary>
    public abstract class SceneBase : MonoBehaviour
    {
        /// <summary>
        /// 씬 로드 시 Unity Awake()에서 항상 1회 호출되는 초기화 훅.
        /// 레퍼런스 캐싱, 초기 상태 구성, 컴포넌트 연결 등 전환과 무관한 준비 작업에 사용한다.
        /// </summary>
        protected virtual void onInitAwake() { }

        protected virtual void Awake()
        {
            onInitAwake();
        }

        /// <summary>
        /// Unity Start 시점. onStart()를 호출한다.
        /// </summary>
        private void Start()
        {
            _ = StartAsync();
        }

        private async Task StartAsync()
        {
            try
            {
                await onStart();
            }
            catch (Exception ex)
            {
                Log.Error($"SceneBase.onStart failed: {ex}");
            }
        }

        // ====================================================================
        // Public facade (SceneTransManager가 호출)
        // ====================================================================

        /// <summary>
        /// 씬 진입 시 호출. SceneTransManager가 씬 로드 완료 후 또는 부팅 시 호출한다.
        /// </summary>
        public async Task Enter()
        {
            await onEnter();
        }

        /// <summary>
        /// 씬 퇴장 시 호출. SceneTransManager가 새 씬 로드 전에 호출한다.
        /// </summary>
        public async Task Exit()
        {
            await onExit();
        }

        // ====================================================================
        // Protected hooks (파생 클래스가 구현)
        // ====================================================================

        /// <summary>
        /// 씬 진입 시 초기화.
        /// </summary>
        protected abstract Task onEnter();

        /// <summary>
        /// 씬 시작 시 호출.
        /// SceneBase.Start()에서 호출된다.
        /// </summary>
        protected virtual Task onStart() => Task.CompletedTask;

        /// <summary>
        /// 씬 퇴장 시 정리.
        /// </summary>
        protected abstract Task onExit();
    }
}
