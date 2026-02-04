// SSOT: skills/devian-unity/30-unity-components/27-bootstrap-resource-object/SKILL.md

using System;
using System.Collections;
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
        /// <summary>
        /// Bootstrap prefab의 기본 Resources 경로.
        /// </summary>
        public const string DefaultPrefabPath = "Devian/Bootstrap";

        private static BaseBootstrap? _instance;
        private static bool _booted;

        /// <summary>
        /// Unity Awake. Ensures required CompoSingleton components exist on Bootstrap.
        /// </summary>
        protected virtual void Awake()
        {
            ensureRequiredComponents();
        }

        /// <summary>
        /// Ensures required CompoSingleton components are attached to Bootstrap.
        /// Override in derived class to add more components.
        /// </summary>
        protected virtual void ensureRequiredComponents()
        {
            ensureComponent<UIManager>();
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
        /// Bootstrap 인스턴스가 생성되었는지 여부.
        /// </summary>
        public static bool IsCreated => _instance != null;

        /// <summary>
        /// BootProc가 완료되었는지 여부.
        /// </summary>
        public static bool IsBooted => _booted;

        /// <summary>
        /// 개발자가 구현할 부트 프로세스.
        /// </summary>
        protected abstract IEnumerator OnBootProc();

        /// <summary>
        /// Resources에서 Bootstrap prefab을 로드하여 인스턴스를 생성한다.
        /// </summary>
        /// <returns>성공 여부</returns>
        public static bool CreateFromResources()
        {
            if (_instance != null)
                return true;

            var prefab = Resources.Load<GameObject>(DefaultPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[BaseBootstrap] Prefab not found at Resources path: {DefaultPrefabPath}");
                return false;
            }

            var go = UnityEngine.Object.Instantiate(prefab);

            // instantiate된 결과에서 BaseBootstrap 찾기
            var bootstraps = go.GetComponentsInChildren<BaseBootstrap>(true);

            if (bootstraps == null || bootstraps.Length == 0)
            {
                Debug.LogError($"[BaseBootstrap] Prefab '{DefaultPrefabPath}' does not contain any BaseBootstrap component");
                return false;
            }

            if (bootstraps.Length > 1)
            {
                Debug.LogError($"[BaseBootstrap] Prefab '{DefaultPrefabPath}' contains multiple BaseBootstrap components ({bootstraps.Length}). Expected exactly 1.");
                return false;
            }

            _instance = bootstraps[0];

            // 부트 컨테이너는 유지되어야 함
            UnityEngine.Object.DontDestroyOnLoad(go);

            return true;
        }

        /// <summary>
        /// BootProc를 실행한다. 1회만 실행된다.
        /// </summary>
        public static IEnumerator BootProc()
        {
            if (_booted)
                yield break;

            if (_instance == null)
            {
                if (!CreateFromResources())
                {
                    Debug.LogError("[BaseBootstrap] BootProc failed: Bootstrap instance not available");
                    yield break;
                }
            }

            if (_instance == null)
            {
                Debug.LogError("[BaseBootstrap] BootProc failed: Bootstrap instance is null after CreateFromResources");
                yield break;
            }

            try
            {
                yield return _instance.OnBootProc();
            }
            finally
            {
                _booted = true;
            }
        }

        /// <summary>
        /// 도메인 리로드 시 상태 리셋.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _booted = false;
        }
    }
}
