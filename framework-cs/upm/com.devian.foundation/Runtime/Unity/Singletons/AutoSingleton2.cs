// SSOT: skills/devian-unity/10-foundation/15-singleton/SKILL.md

using System;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// 2-param AutoSingleton (Registry key = TBase).
    ///
    /// - TBase: MonoBehaviour 기반 Base 타입 (시스템 레이어 접근 키)
    /// - TSelf: 실제 구현 타입 (컨텐츠 레이어 접근)
    ///
    /// 특징:
    /// - 상속 기반이 아니라 정적 helper로 제공한다. (TSelf는 오직 TBase만 상속)
    /// - Instance는 TSelf를 반환하여 캐스팅을 최소화한다.
    /// - 우선순위는 Registry 규칙(Compo > Boot > Auto)을 따른다.
    /// </summary>
    public static class AutoSingleton<TBase, TSelf>
        where TBase : MonoBehaviour
        where TSelf : TBase
    {
        private static readonly object _lock = new object();
        private static bool _isShuttingDown;

        static AutoSingleton()
        {
            Application.quitting += onQuitting;
        }

        private static void onQuitting()
        {
            _isShuttingDown = true;
        }

        /// <summary>
        /// Shutdown 구간 여부. 에디터 종료/플레이 종료/앱 종료 중이면 true.
        /// shutdown 중에는 Instance가 자동 생성을 억제하고 null을 반환한다.
        /// </summary>
        public static bool IsShuttingDown => _isShuttingDown || !Application.isPlaying;

        /// <summary>
        /// 인스턴스 조회 (TSelf 반환). 없으면 자동 생성.
        /// Registry key는 TBase. 시스템 레이어에서는 Singleton.Get&lt;TBase&gt;()를 사용.
        /// </summary>
        public static TSelf Instance
        {
            get
            {
                if (Singleton.TryGet<TBase>(out var baseExisting) && baseExisting != null)
                {
                    return castOrThrow(baseExisting, "AutoSingleton.Instance (registry-hit)");
                }

                lock (_lock)
                {
                    if (Singleton.TryGet<TBase>(out baseExisting) && baseExisting != null)
                    {
                        return castOrThrow(baseExisting, "AutoSingleton.Instance (registry-hit/locked)");
                    }

                    // 1) 씬에서 기존 TSelf 컴포넌트 탐색 (비활성 포함)
                    var found = findExistingInstance();
                    if (found != null)
                    {
                        var debugSource =
                            $"AutoSingleton<{typeof(TBase).Name},{typeof(TSelf).Name}>.Instance (found in scene)";

                        if (!Singleton.Register<TBase>(found, SingletonSource.Auto, debugSource))
                        {
                            // 더 높은 우선순위가 이미 등록됨 - 재조회 반환
                            var adopted = Singleton.Get<TBase>();
                            return castOrThrow(adopted, "AutoSingleton.Instance (adopted)");
                        }

                        UnityEngine.Object.DontDestroyOnLoad(found.gameObject);
                        return found;
                    }

                    // 2) 없으면 생성 (shutdown 중이면 억제)
                    if (IsShuttingDown)
                    {
                        Debug.LogWarning(
                            $"[AutoSingleton] Suppressed auto-create of '{typeof(TSelf).Name}' " +
                            $"(TBase={typeof(TBase).Name}) during shutdown.");
                        return null;
                    }

                    var created = createInstance();

                    var createdDebugSource =
                        $"AutoSingleton<{typeof(TBase).Name},{typeof(TSelf).Name}>.Instance (auto-created)";

                    if (!Singleton.Register<TBase>(created, SingletonSource.Auto, createdDebugSource))
                    {
                        // 생성 도중 더 높은 우선순위가 등록됨 - 생성한 컴포넌트만 정리하고 기존 반환
                        if (created != null)
                        {
                            UnityEngine.Object.Destroy(created);
                        }

                        var adopted = Singleton.Get<TBase>();
                        return castOrThrow(adopted, "AutoSingleton.Instance (adopted-after-create)");
                    }

                    UnityEngine.Object.DontDestroyOnLoad(created.gameObject);
                    return created;
                }
            }
        }

        /// <summary>
        /// 인스턴스 조회 (TSelf 반환). 없으면 false (자동 생성 안 함).
        /// </summary>
        public static bool TryGet(out TSelf value)
        {
            if (Singleton.TryGet<TBase>(out var baseValue) && baseValue != null)
            {
                value = castOrThrow(baseValue, "AutoSingleton.TryGet");
                return true;
            }

            value = null;
            return false;
        }

        private static TSelf castOrThrow(TBase baseValue, string context)
        {
            if (baseValue is TSelf self)
                return self;

            throw new InvalidOperationException(
                $"[AutoSingleton] Registry contains '{typeof(TBase).Name}' but instance type is '{baseValue.GetType().Name}'. " +
                $"Expected '{typeof(TSelf).Name}'. Context: {context}");
        }

        private static TSelf findExistingInstance()
        {
#if UNITY_2023_1_OR_NEWER
            var found = UnityEngine.Object.FindObjectsByType<TSelf>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var found = UnityEngine.Object.FindObjectsOfType<TSelf>(true);
#endif
            return (found != null && found.Length > 0) ? found[0] : null;
        }

        private static TSelf createInstance()
        {
            var go = new GameObject($"[{typeof(TSelf).Name}]");
            return go.AddComponent<TSelf>();
        }
    }
}
