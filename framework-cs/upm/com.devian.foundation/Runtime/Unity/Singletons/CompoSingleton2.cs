// SSOT: skills/devian-unity/10-foundation/15-singleton/SKILL.md

using System;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// 2-param CompoSingleton (Registry key = TBase).
    ///
    /// - 씬/프리팹에 배치된 TSelf가 Awake()에서 Register(this)를 호출해 등록한다.
    /// - Instance는 TSelf를 반환하여 캐스팅을 최소화한다.
    /// - 우선순위는 Registry 규칙(Compo > Boot > Auto)을 따른다.
    /// </summary>
    public static class CompoSingleton<TBase, TSelf>
        where TBase : MonoBehaviour
        where TSelf : TBase
    {
        /// <summary>
        /// 인스턴스 조회 (TSelf 반환). 없으면 예외.
        /// </summary>
        public static TSelf Instance
        {
            get
            {
                var baseValue = Singleton.Get<TBase>();
                return castOrThrow(baseValue, "CompoSingleton.Instance");
            }
        }

        /// <summary>
        /// 인스턴스 조회 (TSelf 반환). 없으면 false.
        /// </summary>
        public static bool TryGet(out TSelf value)
        {
            if (Singleton.TryGet<TBase>(out var baseValue) && baseValue != null)
            {
                value = castOrThrow(baseValue, "CompoSingleton.TryGet");
                return true;
            }

            value = null;
            return false;
        }

        /// <summary>
        /// 씬/프리팹 배치 인스턴스 등록. Awake()에서 호출하는 것을 전제로 한다.
        /// </summary>
        public static void Register(TSelf instance, string debugSource = null)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            var dbg = debugSource ??
                      $"CompoSingleton<{typeof(TBase).Name},{typeof(TSelf).Name}>@{instance.gameObject.scene.name}";

            // Compo는 최고 우선순위. 기존 Auto/Boot가 있으면 Adopt.
            Singleton.Register<TBase>(instance, SingletonSource.Compo, dbg);

            UnityEngine.Object.DontDestroyOnLoad(instance.gameObject);
        }

        /// <summary>
        /// 등록 해제 (선택). OnDestroy에서 호출 가능.
        /// </summary>
        public static void Unregister(TSelf instance)
        {
            if (instance == null)
                return;

            Singleton.Unregister<TBase>(instance);
        }

        private static TSelf castOrThrow(TBase baseValue, string context)
        {
            if (baseValue is TSelf self)
                return self;

            throw new InvalidOperationException(
                $"[CompoSingleton] Registry contains '{typeof(TBase).Name}' but instance type is '{baseValue.GetType().Name}'. " +
                $"Expected '{typeof(TSelf).Name}'. Context: {context}");
        }
    }
}
