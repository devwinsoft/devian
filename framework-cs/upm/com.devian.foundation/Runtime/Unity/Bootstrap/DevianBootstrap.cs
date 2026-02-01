// SSOT: skills/devian-unity/30-unity-components/27-bootstrap-resource-object/SKILL.md

#nullable enable

using UnityEngine;

namespace Devian
{
    /// <summary>
    /// 씬과 무관하게 Bootstrap Root(DDOL)를 준비하는 정적 진입점.
    ///
    /// - Resources에서 Bootstrap Prefab을 찾으면 Instantiate
    /// - 없으면 코드로 Bootstrap Root를 생성(fallback)
    /// - DevianSettings는 Resources에서 로드하며, BootstrapRoot에 주입
    ///
    /// 프레임워크는 "부팅 완료"를 강제/대기하지 않는다. 개발자 코드가 부팅 흐름을 책임진다.
    /// </summary>
    public static class DevianBootstrap
    {
        // Resources.Load 경로 (고정 SSOT)
        // 프로젝트 자산 경로: Assets/Resources/Devian/BootstrapRoot.prefab
        public const string ResourcesPrefabPath = "Devian/BootstrapRoot";

        private static DevianBootstrapRoot? _root;
        private static DevianSettings? _settings;

        /// <summary>
        /// DevianSettings를 Resources에서 로드하여 반환한다.
        /// BootstrapRoot.Settings가 있으면 그것을 우선 사용하고,
        /// 없으면 Resources.Load로 로드한다.
        /// </summary>
        public static DevianSettings? Settings
        {
            get
            {
                // 캐시 반환
                if (_settings != null)
                    return _settings;

                // BootstrapRoot가 있고 Settings를 가지고 있으면 그것을 사용
                Ensure();
                if (_root != null && _root.Settings != null)
                {
                    _settings = _root.Settings;
                    return _settings;
                }

                // Resources에서 로드
                _settings = Resources.Load<DevianSettings>(DevianSettings.ResourcesPath);
                return _settings;
            }
        }

        /// <summary>
        /// 앱 시작 시 씬 로드 이전에 자동 호출된다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnBeforeSceneLoad()
        {
            Ensure();
        }

        /// <summary>
        /// BootstrapRoot 존재 보장 + DDOL + Settings 주입.
        /// </summary>
        public static DevianBootstrapRoot Ensure()
        {
            // 캐시가 유효하면 반환
            if (_root != null)
                return _root;

            // 이미 씬에 존재하는지 탐색(DDOL 포함)
            _root = Object.FindAnyObjectByType<DevianBootstrapRoot>();
            if (_root != null)
            {
                InjectSettingsIfNeeded();
                return _root;
            }

            // 1) Resources Prefab 시도
            var prefab = Resources.Load<GameObject>(ResourcesPrefabPath);
            if (prefab != null)
            {
                var go = Object.Instantiate(prefab);
                go.name = "[Devian] BootstrapRoot";
                Object.DontDestroyOnLoad(go);

                _root = go.GetComponentInChildren<DevianBootstrapRoot>(true);

                if (_root == null)
                    _root = go.AddComponent<DevianBootstrapRoot>();

                InjectSettingsIfNeeded();
                return _root;
            }

            // 2) Fallback 생성(테스트/최소 실행 보장)
            var root = new GameObject("[Devian] BootstrapRoot");
            Object.DontDestroyOnLoad(root);

            _root = root.AddComponent<DevianBootstrapRoot>();

            InjectSettingsIfNeeded();

            Debug.LogWarning(
                $"DevianBootstrap: Resources prefab not found at '{ResourcesPrefabPath}'. " +
                $"Created fallback BootstrapRoot. Use menu 'Devian/Create Bootstrap' to create the prefab.");

            return _root;
        }

        /// <summary>
        /// BootstrapRoot.Settings가 null이면 Resources에서 로드하여 주입한다.
        /// </summary>
        private static void InjectSettingsIfNeeded()
        {
            if (_root == null)
                return;

            if (_root.Settings != null)
            {
                _settings = _root.Settings;
                return;
            }

            // Resources에서 Settings 로드
            var loaded = Resources.Load<DevianSettings>(DevianSettings.ResourcesPath);
            if (loaded != null)
            {
                _root.SetSettings(loaded);
                _settings = loaded;
            }
            else
            {
                Debug.LogWarning(
                    $"DevianBootstrap: DevianSettings not found at Resources '{DevianSettings.ResourcesPath}'. " +
                    $"Use menu 'Devian/Create Bootstrap' to create the settings asset.");
            }
        }
    }
}
