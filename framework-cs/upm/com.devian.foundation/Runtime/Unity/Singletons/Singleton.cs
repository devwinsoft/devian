// SSOT: skills/devian-unity/30-unity-components/31-singleton/SKILL.md

namespace Devian
{
    /// <summary>
    /// 싱글톤 접근 파사드.
    /// 모든 싱글톤 접근은 이 파사드를 통해 수행.
    /// </summary>
    public static class Singleton
    {
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
    }
}
