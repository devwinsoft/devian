using System;
using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Game;

namespace Devian
{
    /// <summary>
    /// 광고 provider 공통 인터페이스.
    /// AdsManager는 이 인터페이스만 사용하며, GoogleMobileAds.* 타입을 직접 참조하지 않는다.
    /// </summary>
    public interface IAdProvider
    {
        /// <summary>SDK 초기화. Idempotent.</summary>
        Task<bool> InitializeAsync(CancellationToken ct = default);

        /// <summary>광고 preload.</summary>
        Task<bool> LoadAsync(string advertiseId, ADVERTISE_FORMAT format, string adUnitId, CancellationToken ct = default);

        /// <summary>광고 표시. 풀스크린은 close까지 await하지 않는다 (OnClosed로 통지).</summary>
        Task<AdProviderShowResult> ShowAsync(string advertiseId, ADVERTISE_FORMAT format, CancellationToken ct = default);

        /// <summary>배너 숨김. 동기 — Banner hide는 즉시 완료.</summary>
        void Hide(string advertiseId, ADVERTISE_FORMAT format);

        /// <summary>
        /// Rewarded SSV용 데이터 설정. Show 직전에 호출한다.
        /// provider가 SSV를 지원하지 않으면 무시한다.
        /// </summary>
        void SetRewardedSsvData(string advertiseId, string userId, string customData);

        event Action<string> OnLoaded;
        event Action<string, string> OnFailedToLoad;
        event Action<string> OnOpened;
        event Action<string> OnClosed;
        event Action<string> OnRewardEarned;
    }

    public enum AdProviderShowResult
    {
        Shown,
        NotReady,
        Failed,
        Skipped,
    }
}
