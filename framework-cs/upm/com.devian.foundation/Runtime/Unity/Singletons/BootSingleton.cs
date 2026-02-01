// SSOT: skills/devian-unity/30-unity-components/31-singleton/SKILL.md

using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Bootstrap 프리팹용 싱글톤 컴포넌트 베이스.
    /// BootstrapRoot.prefab에 붙여서 사용한다.
    /// Awake()에서 SingletonRegistry에 Source=Boot로 등록한다.
    /// 우선순위: Compo > Boot > Auto
    ///
    /// 동일 타입 Boot 중복은 즉시 예외.
    /// 씬에서 CompoSingleton으로 같은 타입이 등장하면 Boot를 Adopt(대체)할 수 있다.
    /// Adopt 시 BootstrapRoot GameObject 전체를 파괴하면 안 된다 (컴포넌트만 제거).
    /// </summary>
    public abstract class BootSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        /// <summary>
        /// DontDestroyOnLoad 적용 여부. 기본 true.
        /// BootstrapRoot가 DDOL인 경우에도 중복 적용은 문제 없다.
        /// </summary>
        protected virtual bool DontDestroy => true;

        /// <summary>
        /// 인스턴스 조회. 없으면 예외 (auto-create 안 함).
        /// </summary>
        public static T Instance => Singleton.Get<T>();

        /// <summary>
        /// 인스턴스 조회. 없으면 false (auto-create 안 함).
        /// </summary>
        public static bool TryGet(out T value) => Singleton.TryGet(out value);

        protected virtual void Awake()
        {
            var self = (T)(object)this;
            var sceneName = gameObject.scene.name ?? "unknown";
            var debugSource = $"BootSingleton<{typeof(T).Name}>@{sceneName}";

            // Registry에 Boot로 등록
            if (!Singleton.Register(self, SingletonSource.Boot, debugSource))
            {
                // 등록 실패 (Compo가 이미 등록됨) - 자신(컴포넌트)만 파괴 (GameObject 전체 파괴 금지)
                Debug.LogWarning(
                    $"[BootSingleton] Destroying '{typeof(T).Name}' (higher priority already registered)");
                Destroy(this);
                return;
            }

            if (DontDestroy)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        protected virtual void OnDestroy()
        {
            // 현재 인스턴스일 때만 해제
            Singleton.Unregister((T)(object)this);
        }
    }
}
