namespace Devian
{
    /// <summary>
    /// 앱 버전 체크 결과.
    /// MobileApplication.VersionCheck()의 반환값으로 사용한다.
    /// </summary>
    public enum VersionCheckResult
    {
        /// <summary>현재 버전이 최신이거나 문제 없음.</summary>
        Success,

        /// <summary>currentVersion 미만. 플레이 가능하지만 업데이트 권고.</summary>
        RecommendUpdate,

        /// <summary>minVersion 미만. 해당 버전 이상으로 업데이트 필수 (플레이 불가).</summary>
        ForceUpdate,
    }
}
