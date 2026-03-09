// SSOT: skills/devian-unity/11-common-system/29-singleton/SKILL.md

using UnityEngine;

namespace Devian
{
    /// <summary>
    /// 싱글톤 접근 파사드.
    /// 모든 싱글톤 접근은 이 파사드를 통해 수행.
    /// </summary>
    public static class Singleton
    {
        /// <summary>
        /// Shutdown 구간 여부. 앱 종료/플레이 종료 중이면 true.
        /// BaseApplication.IsApplicationQuitting에 위임한다.
        /// Create/CreateFromResources 계열은 shutdown 중 생성을 억제한다.
        /// </summary>
        public static bool IsShuttingDown => BaseApplication.IsApplicationQuitting || !Application.isPlaying;

        /// <summary>
        /// 인스턴스 조회. 없으면 예외.
        /// </summary>
        public static T Get<T>()
        {
            return SingletonRegistry.Get<T>();
        }

        /// <summary>
        /// 인스턴스 조회. 없으면 false.
        /// </summary>
        public static bool TryGet<T>(out T instance)
        {
            return SingletonRegistry.TryGet(out instance);
        }

        /// <summary>
        /// 인스턴스 등록.
        /// </summary>
        /// <param name="instance">등록할 인스턴스</param>
        /// <param name="source">등록 소스 (Auto/Boot/Compo)</param>
        /// <param name="debugSource">디버그용 소스 정보</param>
        /// <returns>true면 등록 성공, false면 기존 인스턴스 유지</returns>
        public static bool Register<T>(T instance, SingletonSource source, string debugSource = null)
        {
            return SingletonRegistry.Register(instance, source, debugSource);
        }

        /// <summary>
        /// 인스턴스 등록 해제.
        /// </summary>
        public static void Unregister<T>(T instance)
        {
            SingletonRegistry.Unregister(instance);
        }

        /// <summary>
        /// GameObject 생성 + AddComponent + Registry 등록(Boot). key=T.
        /// 이미 등록되어 있으면 기존 인스턴스 반환.
        /// IL2CPP 안전: 리플렉션 없음.
        /// </summary>
        public static T Create<T>() where T : MonoBehaviour
        {
            if (TryGet<T>(out var existing))
                return existing;

            if (IsShuttingDown)
            {
                Debug.LogWarning(
                    $"[Singleton] Suppressed Create<{typeof(T).Name}> during shutdown.");
                return null;
            }

            var instance = CreateAndAddComponent<T>();
            if (instance == null)
                return null;

            var debugSource = $"Singleton.Create<{typeof(T).Name}>()";
            if (!Register(instance, SingletonSource.Boot, debugSource))
            {
                Object.Destroy(instance.gameObject);
                return TryGet<T>(out var adopted) ? adopted : null;
            }

            Object.DontDestroyOnLoad(instance.gameObject);
            return instance;
        }

        /// <summary>
        /// GameObject 생성 + AddComponent + Registry 등록(Boot). key=TBase.
        /// 이미 등록되어 있으면 기존 인스턴스 반환 (TSelf로 캐스팅).
        /// IL2CPP 안전: 리플렉션 없음.
        /// </summary>
        public static TSelf Create<TBase, TSelf>()
            where TBase : MonoBehaviour
            where TSelf : TBase
        {
            if (TryGet<TBase>(out var baseExisting) && baseExisting is TSelf existingSelf)
                return existingSelf;

            if (IsShuttingDown)
            {
                Debug.LogWarning(
                    $"[Singleton] Suppressed Create<{typeof(TBase).Name},{typeof(TSelf).Name}> during shutdown.");
                return null;
            }

            var instance = CreateAndAddComponent<TSelf>();
            if (instance == null)
                return null;

            var debugSource = $"Singleton.Create<{typeof(TBase).Name},{typeof(TSelf).Name}>()";
            if (!Register<TBase>(instance, SingletonSource.Boot, debugSource))
            {
                Object.Destroy(instance.gameObject);
                if (TryGet<TBase>(out var adopted) && adopted is TSelf adoptedSelf)
                    return adoptedSelf;
                return null;
            }

            Object.DontDestroyOnLoad(instance.gameObject);
            return instance;
        }

        /// <summary>
        /// Resources에서 프리팹 로드 + Registry 등록(Boot). key=T.
        /// 이미 등록되어 있으면 기존 인스턴스 반환.
        /// IL2CPP 안전: 리플렉션 없음.
        /// </summary>
        /// <param name="resourcePath">Resources 경로 (예: "Singletons/MyManager")</param>
        public static T CreateFromResources<T>(string resourcePath) where T : MonoBehaviour
        {
            if (TryGet<T>(out var existing))
                return existing;

            if (IsShuttingDown)
            {
                Debug.LogWarning(
                    $"[Singleton] Suppressed CreateFromResources<{typeof(T).Name}> during shutdown.");
                return null;
            }

            var instance = LoadAndInstantiate<T>(resourcePath);
            if (instance == null)
                return null;

            var debugSource = $"Singleton.CreateFromResources<{typeof(T).Name}>(\"{resourcePath}\")";
            if (!Register(instance, SingletonSource.Boot, debugSource))
            {
                Object.Destroy(instance.gameObject);
                return TryGet<T>(out var adopted) ? adopted : null;
            }

            Object.DontDestroyOnLoad(instance.gameObject);
            return instance;
        }

        /// <summary>
        /// Resources에서 프리팹 로드 + Registry 등록(Boot). key=TBase.
        /// 이미 등록되어 있으면 기존 인스턴스 반환 (TSelf로 캐스팅).
        /// IL2CPP 안전: 리플렉션 없음.
        /// </summary>
        /// <param name="resourcePath">Resources 경로 (예: "Singletons/MyManager")</param>
        public static TSelf CreateFromResources<TBase, TSelf>(string resourcePath)
            where TBase : MonoBehaviour
            where TSelf : TBase
        {
            if (TryGet<TBase>(out var baseExisting) && baseExisting is TSelf existingSelf)
                return existingSelf;

            if (IsShuttingDown)
            {
                Debug.LogWarning(
                    $"[Singleton] Suppressed CreateFromResources<{typeof(TBase).Name},{typeof(TSelf).Name}> during shutdown.");
                return null;
            }

            var instance = LoadAndInstantiate<TSelf>(resourcePath);
            if (instance == null)
                return null;

            var debugSource =
                $"Singleton.CreateFromResources<{typeof(TBase).Name},{typeof(TSelf).Name}>(\"{resourcePath}\")";
            if (!Register<TBase>(instance, SingletonSource.Boot, debugSource))
            {
                Object.Destroy(instance.gameObject);
                if (TryGet<TBase>(out var adopted) && adopted is TSelf adoptedSelf)
                    return adoptedSelf;
                return null;
            }

            Object.DontDestroyOnLoad(instance.gameObject);
            return instance;
        }

        /// <summary>
        /// 빈 GameObject 생성 + AddComponent.
        /// </summary>
        private static T CreateAndAddComponent<T>() where T : MonoBehaviour
        {
            var go = new GameObject($"[{typeof(T).Name}]");
            var instance = go.AddComponent<T>();
            return instance;
        }

        /// <summary>
        /// Resources에서 GameObject 프리팹 로드 + Instantiate + GetComponent.
        /// </summary>
        private static T LoadAndInstantiate<T>(string resourcePath) where T : MonoBehaviour
        {
            var prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
            {
                Debug.LogError(
                    $"[Singleton] Failed to load prefab at Resources path: '{resourcePath}'. " +
                    "Ensure the prefab exists under Assets/Resources/.");
                return null;
            }

            var comp = prefab.GetComponent<T>();
            if (comp == null)
            {
                Debug.LogError(
                    $"[Singleton] Prefab at '{resourcePath}' does not have component '{typeof(T).Name}'.");
                return null;
            }

            var go = Object.Instantiate(prefab);
            go.name = $"[{typeof(T).Name}]";

            var instance = go.GetComponent<T>();
            return instance;
        }
    }
}
