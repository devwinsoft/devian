namespace Devian.Core
{
    /// <summary>
    /// Table loading mode.
    /// </summary>
    public enum LoadMode
    {
        /// <summary>
        /// 기존 캐시 유지 + 새 데이터 병합. key 충돌 시 overwrite.
        /// </summary>
        Merge,

        /// <summary>
        /// 기존 캐시 Clear 후 로드.
        /// </summary>
        Replace
    }
}
