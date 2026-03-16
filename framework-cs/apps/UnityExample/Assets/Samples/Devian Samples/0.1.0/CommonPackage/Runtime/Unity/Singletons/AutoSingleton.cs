// SSOT: skills/devian-unity/11-common-system/29-singleton/SKILL.md

using UnityEngine;

namespace Devian
{
    /// <summary>
    /// 코드 생성 전용 싱글톤. Instance 접근 시 없으면 자동 생성한다.
    /// 씬/프리팹에 미리 부착해서 사용하는 패턴은 지원하지 않는다.
    /// 우선순위 최저: Compo/Boot가 등록되면 대체(Adopt)됨.
    /// </summary>
    public abstract class AutoSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static readonly object _lock = new object();
        private static bool _isCreating;

        /// <summary>
        /// Shutdown 구간 여부. Singleton.IsShuttingDown에 위임.
        /// shutdown 중에는 Instance가 자동 생성을 억제하고 null을 반환한다.
        /// </summary>
        public static bool IsShuttingDown => Singleton.IsShuttingDown;

        /// <summary>
        /// DontDestroyOnLoad 적용 여부. 기본 true.
        /// </summary>
        protected virtual bool DontDestroy => true;

        /// <summary>
        /// 인스턴스 조회. 없으면 자동 생성.
        /// </summary>
        public static T Instance
        {
            get
            {
                // 1. Registry에서 조회 (Compo/Boot/Auto 모두 포함)
                if (Singleton.TryGet<T>(out var existing))
                {
                    return existing;
                }

                lock (_lock)
                {
                    // Double-check inside lock
                    if (Singleton.TryGet<T>(out existing))
                    {
                        return existing;
                    }

                    // 2. 없으면 생성 (shutdown 중이면 억제)
                    if (IsShuttingDown)
                    {
                        Debug.LogWarning($"[AutoSingleton] Suppressed auto-create of '{typeof(T).Name}' during shutdown.");
                        return null;
                    }

                    _isCreating = true;
                    try
                    {
                        // CreateInstance() 내부에서 AddComponent<T>()가 호출되면
                        // Unity가 Awake()를 호출하고, Awake()가 Registry 등록을 수행한다.
                        CreateInstance();
                    }
                    finally
                    {
                        _isCreating = false;
                    }

                    // Awake()에서 등록이 완료되었으므로 Singleton.Get<T>()로 반환
                    return Singleton.Get<T>();
                }
            }
        }

        /// <summary>
        /// 인스턴스 조회. 없으면 false (자동 생성 안 함).
        /// </summary>
        public static bool TryGet(out T value) => Singleton.TryGet(out value);

        /// <summary>
        /// 새 인스턴스 생성.
        /// </summary>
        private static T CreateInstance()
        {
            var go = new GameObject($"[{typeof(T).Name}]");
            return go.AddComponent<T>();
        }

        protected void Awake()
        {
            var self = (T)(object)this;

            if (!_isCreating)
            {
                Debug.LogError(
                    $"[AutoSingleton] '{typeof(T).Name}' must be created via {typeof(T).Name}.Instance or framework script code. " +
                    "Attaching AutoSingleton components to scene/prefab objects is not supported.");
                Destroy(this);
                return;
            }

            // 이미 Registry에 등록된 인스턴스가 있는지 확인
            if (SingletonRegistry.TryGetWithSource<T>(out var existing, out var existingSource))
            {
                if (!ReferenceEquals(existing, self))
                {
                    // 이미 다른 인스턴스가 등록됨 - 자신(컴포넌트)만 파괴 (GameObject 전체 파괴 금지)
                    Debug.LogWarning(
                        $"[AutoSingleton] Destroying duplicate '{typeof(T).Name}' instance. " +
                        $"Existing source: {existingSource}");
                    Destroy(this);
                    return;
                }
                // 이미 자신이 등록됨 (Instance 접근으로 생성된 경우)
                if (DontDestroy)
                {
                    DontDestroyOnLoad(gameObject);
                }
                onInitAwake();
                return;
            }

            // 아직 등록 안 됨 - 등록 시도
            var debugSource = $"AutoSingleton<{typeof(T).Name}>.Awake";
            if (!Singleton.Register(self, SingletonSource.Auto, debugSource))
            {
                // 등록 실패 (더 높은 우선순위가 이미 있음) - 자신(컴포넌트)만 파괴 (GameObject 전체 파괴 금지)
                Debug.LogWarning(
                    $"[AutoSingleton] Destroying '{typeof(T).Name}' (higher priority already registered)");
                Destroy(this);
                return;
            }

            if (DontDestroy)
            {
                DontDestroyOnLoad(gameObject);
            }

            onInitAwake();
        }

        protected virtual void onInitAwake() { }

        protected void OnDestroy()
        {
            onDestroy();
            // 현재 인스턴스일 때만 해제
            Singleton.Unregister((T)(object)this);
        }

        protected virtual void onDestroy() { }
    }
}
