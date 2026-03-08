// SSOT: skills/devian-unity/11-common-system/29-singleton/SKILL.md

using UnityEngine;

namespace Devian
{
    /// <summary>
    /// 씬/프리팹에 미리 부착된 컴포넌트로 초기화하는 싱글톤.
    /// 런타임 AddComponent로 생성하는 경로는 패턴 위반이다.
    /// 우선순위 최고: CompoSingleton이 등록되면 같은 타입의 Auto/Boot 인스턴스를 대체(Adopt).
    /// Compo 중복은 즉시 예외.
    /// </summary>
    public abstract class CompoSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        /// <summary>
        /// DontDestroyOnLoad 적용 여부. 기본 true.
        /// </summary>
        protected virtual bool DontDestroy => true;

        /// <summary>
        /// 인스턴스 조회. 없으면 예외.
        /// </summary>
        public static T Instance => Singleton.Get<T>();

        /// <summary>
        /// 인스턴스 조회. 없으면 false.
        /// </summary>
        public static bool TryGet(out T value) => Singleton.TryGet(out value);

        protected void Awake()
        {
            var self = (T)(object)this;
            var debugSource = $"CompoSingleton<{typeof(T).Name}>@{gameObject.scene.name}";

            // Compo는 최고 우선순위 - 기존 Auto/Boot를 대체(Adopt)
            // Compo 중복은 Registry에서 예외 발생
            Singleton.Register(self, SingletonSource.Compo, debugSource);

            if (DontDestroy)
            {
                DontDestroyOnLoad(gameObject);
            }

            onInitAwake();
        }

        protected virtual void onInitAwake() { }

        protected virtual void OnDestroy()
        {
            // 현재 인스턴스일 때만 해제
            Singleton.Unregister((T)(object)this);
        }
    }
}
