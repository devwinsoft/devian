// SSOT: skills/devian-unity/10-foundation/16-bootstrap/SKILL.md

using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Bootstrap 프리팹용 추상 베이스.
    /// 개발자는 모듈 밖(asmdef)에서 BaseBootstrap 파생 클래스를 선언하고,
    /// 그 컴포넌트를 Bootstrap prefab에 붙여서 사용한다.
    ///
    /// Bootstrap/BootProc/씬 구성은 개발자가 처리한다.
    /// 프레임워크는 BaseScene 로드 시 Bootstrap prefab을 자동 instantiate하고,
    /// OnEnter/OnStart 직전에 BootProc를 1회 보장한다.
    /// </summary>
    public abstract class BaseBootstrap : MonoBehaviour
    {
        private static BaseBootstrap _instance;
        private static bool _booted;

        /// <summary>
        /// 에디터 종료/플레이 종료/씬 종료 정리 단계 여부.
        /// 정리 경로에서 싱글톤/매니저 접근을 스킵하는 데 사용한다.
        /// </summary>
        public static bool IsShuttingDown { get; private set; }

        /// <summary>
        /// Unity Awake. Ensures required CompoSingleton components exist on Bootstrap.
        /// </summary>
        protected virtual void Awake()
        {
            if (_instance == null) _instance = this;
            ensureRequiredComponents();
        }

        /// <summary>
        /// Ensures required CompoSingleton components are attached to Bootstrap.
        /// Override in derived class to add more components.
        /// </summary>
        protected virtual void ensureRequiredComponents()
        {
            ensureComponent<InputManager>();
        }

        /// <summary>
        /// Gets or adds a component of type T to this GameObject.
        /// </summary>
        private T ensureComponent<T>() where T : Component
        {
            var c = GetComponent<T>();
            if (c != null) return c;
            return gameObject.AddComponent<T>();
        }

        /// <summary>
        /// Bootstrap 인스턴스 참조.
        /// </summary>
        public static BaseBootstrap Instance => _instance;

        /// <summary>
        /// 개발자가 구현할 부트 프로세스.
        /// </summary>
        protected abstract Task OnBootProc();

        /// <summary>
        /// BootProc를 실행한다. 1회만 실행된다.
        /// </summary>
        public async Task BootProc()
        {
            if (_booted)
                return;

            try
            {
                await OnBootProc();
            }
            finally
            {
                _booted = true;
            }
        }

        /// <summary>
        /// 도메인 리로드 시 상태 리셋.
        /// </summary>
        protected virtual void OnApplicationQuit()
        {
            IsShuttingDown = true;
        }

        protected virtual void OnDestroy()
        {
            IsShuttingDown = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _booted = false;
            IsShuttingDown = false;
        }
    }
}
