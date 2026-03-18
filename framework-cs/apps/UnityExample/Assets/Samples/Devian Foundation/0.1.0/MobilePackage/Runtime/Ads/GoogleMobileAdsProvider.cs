using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using GoogleMobileAds.Api;
using Devian.Domain.Game;

namespace Devian
{
    /// <summary>
    /// Google Mobile Ads Unity Plugin v11 provider adapter.
    /// SDK 타입은 이 파일 안에서만 사용한다 — 상위 로직은 IAdProvider만 참조.
    /// </summary>
    public sealed class GoogleMobileAdsProvider : IAdProvider
    {
        const string Tag = "GoogleMobileAdsProvider";

        readonly Dictionary<string, BannerView> _banners = new Dictionary<string, BannerView>();
        readonly Dictionary<string, InterstitialAd> _interstitials = new Dictionary<string, InterstitialAd>();
        readonly Dictionary<string, RewardedAd> _rewardedAds = new Dictionary<string, RewardedAd>();
        readonly Dictionary<string, AppOpenAd> _appOpenAds = new Dictionary<string, AppOpenAd>();
        readonly Dictionary<string, (string userId, string customData)> _ssvData
            = new Dictionary<string, (string, string)>();

        bool _sdkInitialized;

        public event Action<string> OnLoaded;
        public event Action<string, string> OnFailedToLoad;
        public event Action<string> OnOpened;
        public event Action<string> OnClosed;
        public event Action<string> OnRewardEarned;

        // ────────────────────────────────────────────
        // InitializeAsync
        // ────────────────────────────────────────────

        public Task<bool> InitializeAsync(CancellationToken ct = default)
        {
            if (_sdkInitialized)
                return Task.FromResult(true);

            var tcs = new TaskCompletionSource<bool>();

            try
            {
                MobileAds.Initialize(status =>
                {
                    _sdkInitialized = true;
                    Debug.Log($"[{Tag}] SDK initialized");
                    tcs.TrySetResult(true);
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Tag}] Initialize failed: {ex.Message}");
                tcs.TrySetResult(false);
            }

            return tcs.Task;
        }

        // ────────────────────────────────────────────
        // LoadAsync
        // ────────────────────────────────────────────

        public Task<bool> LoadAsync(
            string advertiseId,
            ADVERTISE_FORMAT format,
            string adUnitId,
            CancellationToken ct = default)
        {
            switch (format)
            {
                case ADVERTISE_FORMAT.BANNER:
                    return LoadBannerAsync(advertiseId, adUnitId);
                case ADVERTISE_FORMAT.INTERSTITIAL:
                    return LoadInterstitialAsync(advertiseId, adUnitId);
                case ADVERTISE_FORMAT.REWARDED:
                    return LoadRewardedAsync(advertiseId, adUnitId);
                case ADVERTISE_FORMAT.APP_OPEN:
                    return LoadAppOpenAsync(advertiseId, adUnitId);
                default:
                    Debug.LogWarning($"[{Tag}] Unknown format: {format}");
                    return Task.FromResult(false);
            }
        }

        Task<bool> LoadBannerAsync(string advertiseId, string adUnitId)
        {
            try
            {
                DestroyBanner(advertiseId);

                var bannerView = new BannerView(adUnitId, AdSize.Banner, AdPosition.Bottom);

                bannerView.OnBannerAdLoaded += () =>
                {
                    Debug.Log($"[{Tag}] Banner loaded id={advertiseId}");
                    OnLoaded?.Invoke(advertiseId);
                };
                bannerView.OnBannerAdLoadFailed += (LoadAdError error) =>
                {
                    Debug.LogWarning($"[{Tag}] Banner load failed id={advertiseId}: {error.GetMessage()}");
                    OnFailedToLoad?.Invoke(advertiseId, error.GetMessage());
                };
                bannerView.OnAdPaid += (AdValue _) => { };
                bannerView.OnAdFullScreenContentOpened += () => OnOpened?.Invoke(advertiseId);
                bannerView.OnAdFullScreenContentClosed += () => OnClosed?.Invoke(advertiseId);

                _banners[advertiseId] = bannerView;
                bannerView.LoadAd(new AdRequest());
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Tag}] Banner load exception: {ex.Message}");
                OnFailedToLoad?.Invoke(advertiseId, ex.Message);
                return Task.FromResult(false);
            }
        }

        Task<bool> LoadInterstitialAsync(string advertiseId, string adUnitId)
        {
            var tcs = new TaskCompletionSource<bool>();

            try
            {
                DestroyInterstitial(advertiseId);

                InterstitialAd.Load(adUnitId, new AdRequest(), (InterstitialAd ad, LoadAdError error) =>
                {
                    if (error != null || ad == null)
                    {
                        var msg = error != null ? error.GetMessage() : "ad is null";
                        Debug.LogWarning($"[{Tag}] Interstitial load failed id={advertiseId}: {msg}");
                        OnFailedToLoad?.Invoke(advertiseId, msg);
                        tcs.TrySetResult(false);
                        return;
                    }

                    WireFullScreenEvents(ad, advertiseId, () =>
                    {
                        _interstitials.Remove(advertiseId);
                    });

                    _interstitials[advertiseId] = ad;
                    Debug.Log($"[{Tag}] Interstitial loaded id={advertiseId}");
                    OnLoaded?.Invoke(advertiseId);
                    tcs.TrySetResult(true);
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Tag}] Interstitial load exception: {ex.Message}");
                OnFailedToLoad?.Invoke(advertiseId, ex.Message);
                tcs.TrySetResult(false);
            }

            return tcs.Task;
        }

        Task<bool> LoadRewardedAsync(string advertiseId, string adUnitId)
        {
            var tcs = new TaskCompletionSource<bool>();

            try
            {
                DestroyRewarded(advertiseId);

                RewardedAd.Load(adUnitId, new AdRequest(), (RewardedAd ad, LoadAdError error) =>
                {
                    if (error != null || ad == null)
                    {
                        var msg = error != null ? error.GetMessage() : "ad is null";
                        Debug.LogWarning($"[{Tag}] Rewarded load failed id={advertiseId}: {msg}");
                        OnFailedToLoad?.Invoke(advertiseId, msg);
                        tcs.TrySetResult(false);
                        return;
                    }

                    WireFullScreenEvents(ad, advertiseId, () =>
                    {
                        _rewardedAds.Remove(advertiseId);
                    });

                    _rewardedAds[advertiseId] = ad;
                    Debug.Log($"[{Tag}] Rewarded loaded id={advertiseId}");
                    OnLoaded?.Invoke(advertiseId);
                    tcs.TrySetResult(true);
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Tag}] Rewarded load exception: {ex.Message}");
                OnFailedToLoad?.Invoke(advertiseId, ex.Message);
                tcs.TrySetResult(false);
            }

            return tcs.Task;
        }

        Task<bool> LoadAppOpenAsync(string advertiseId, string adUnitId)
        {
            var tcs = new TaskCompletionSource<bool>();

            try
            {
                DestroyAppOpen(advertiseId);

                AppOpenAd.Load(adUnitId, new AdRequest(), (AppOpenAd ad, LoadAdError error) =>
                {
                    if (error != null || ad == null)
                    {
                        var msg = error != null ? error.GetMessage() : "ad is null";
                        Debug.LogWarning($"[{Tag}] AppOpen load failed id={advertiseId}: {msg}");
                        OnFailedToLoad?.Invoke(advertiseId, msg);
                        tcs.TrySetResult(false);
                        return;
                    }

                    WireFullScreenEvents(ad, advertiseId, () =>
                    {
                        _appOpenAds.Remove(advertiseId);
                    });

                    _appOpenAds[advertiseId] = ad;
                    Debug.Log($"[{Tag}] AppOpen loaded id={advertiseId}");
                    OnLoaded?.Invoke(advertiseId);
                    tcs.TrySetResult(true);
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Tag}] AppOpen load exception: {ex.Message}");
                OnFailedToLoad?.Invoke(advertiseId, ex.Message);
                tcs.TrySetResult(false);
            }

            return tcs.Task;
        }

        // ────────────────────────────────────────────
        // ShowAsync
        // ────────────────────────────────────────────

        public Task<AdProviderShowResult> ShowAsync(
            string advertiseId,
            ADVERTISE_FORMAT format,
            CancellationToken ct = default)
        {
            try
            {
                switch (format)
                {
                    case ADVERTISE_FORMAT.BANNER:
                        return ShowBanner(advertiseId);
                    case ADVERTISE_FORMAT.INTERSTITIAL:
                        return ShowInterstitial(advertiseId);
                    case ADVERTISE_FORMAT.REWARDED:
                        return ShowRewarded(advertiseId);
                    case ADVERTISE_FORMAT.APP_OPEN:
                        return ShowAppOpen(advertiseId);
                    default:
                        return Task.FromResult(AdProviderShowResult.Failed);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Tag}] Show exception id={advertiseId}: {ex.Message}");
                return Task.FromResult(AdProviderShowResult.Failed);
            }
        }

        Task<AdProviderShowResult> ShowBanner(string advertiseId)
        {
            if (!_banners.TryGetValue(advertiseId, out var banner))
                return Task.FromResult(AdProviderShowResult.NotReady);

            banner.Show();
            return Task.FromResult(AdProviderShowResult.Shown);
        }

        Task<AdProviderShowResult> ShowInterstitial(string advertiseId)
        {
            if (!_interstitials.TryGetValue(advertiseId, out var ad) || !ad.CanShowAd())
                return Task.FromResult(AdProviderShowResult.NotReady);

            ad.Show();
            return Task.FromResult(AdProviderShowResult.Shown);
        }

        Task<AdProviderShowResult> ShowRewarded(string advertiseId)
        {
            if (!_rewardedAds.TryGetValue(advertiseId, out var ad) || !ad.CanShowAd())
                return Task.FromResult(AdProviderShowResult.NotReady);

            // SSV 옵션 적용 (Show 직전)
            if (_ssvData.TryGetValue(advertiseId, out var ssv))
            {
                var options = new ServerSideVerificationOptions
                {
                    UserId = ssv.userId,
                    CustomData = ssv.customData,
                };
                ad.SetServerSideVerificationOptions(options);
                _ssvData.Remove(advertiseId);
            }

            ad.Show((Reward reward) =>
            {
                Debug.Log($"[{Tag}] Reward earned id={advertiseId} type={reward.Type} amount={reward.Amount}");
                OnRewardEarned?.Invoke(advertiseId);
            });

            return Task.FromResult(AdProviderShowResult.Shown);
        }

        Task<AdProviderShowResult> ShowAppOpen(string advertiseId)
        {
            if (!_appOpenAds.TryGetValue(advertiseId, out var ad) || !ad.CanShowAd())
                return Task.FromResult(AdProviderShowResult.NotReady);

            ad.Show();
            return Task.FromResult(AdProviderShowResult.Shown);
        }

        // ────────────────────────────────────────────
        // Hide (동기)
        // ────────────────────────────────────────────

        public void Hide(string advertiseId, ADVERTISE_FORMAT format)
        {
            if (format == ADVERTISE_FORMAT.BANNER && _banners.TryGetValue(advertiseId, out var banner))
            {
                banner.Hide();
            }
        }

        // ────────────────────────────────────────────
        // SSV
        // ────────────────────────────────────────────

        public void SetRewardedSsvData(string advertiseId, string userId, string customData)
        {
            _ssvData[advertiseId] = (userId, customData);
        }

        // ────────────────────────────────────────────
        // Helpers
        // ────────────────────────────────────────────

        void WireFullScreenEvents(InterstitialAd ad, string advertiseId, Action onCleanup)
        {
            ad.OnAdFullScreenContentOpened += () => OnOpened?.Invoke(advertiseId);
            ad.OnAdFullScreenContentClosed += () =>
            {
                OnClosed?.Invoke(advertiseId);
                ad.Destroy();
                onCleanup?.Invoke();
            };
            ad.OnAdFullScreenContentFailed += (AdError error) =>
            {
                Debug.LogWarning($"[{Tag}] Interstitial show failed id={advertiseId}: {error.GetMessage()}");
                OnClosed?.Invoke(advertiseId);
                ad.Destroy();
                onCleanup?.Invoke();
            };
        }

        void WireFullScreenEvents(RewardedAd ad, string advertiseId, Action onCleanup)
        {
            ad.OnAdFullScreenContentOpened += () => OnOpened?.Invoke(advertiseId);
            ad.OnAdFullScreenContentClosed += () =>
            {
                OnClosed?.Invoke(advertiseId);
                ad.Destroy();
                onCleanup?.Invoke();
            };
            ad.OnAdFullScreenContentFailed += (AdError error) =>
            {
                Debug.LogWarning($"[{Tag}] Rewarded show failed id={advertiseId}: {error.GetMessage()}");
                OnClosed?.Invoke(advertiseId);
                ad.Destroy();
                onCleanup?.Invoke();
            };
        }

        void WireFullScreenEvents(AppOpenAd ad, string advertiseId, Action onCleanup)
        {
            ad.OnAdFullScreenContentOpened += () => OnOpened?.Invoke(advertiseId);
            ad.OnAdFullScreenContentClosed += () =>
            {
                OnClosed?.Invoke(advertiseId);
                ad.Destroy();
                onCleanup?.Invoke();
            };
            ad.OnAdFullScreenContentFailed += (AdError error) =>
            {
                Debug.LogWarning($"[{Tag}] AppOpen show failed id={advertiseId}: {error.GetMessage()}");
                OnClosed?.Invoke(advertiseId);
                ad.Destroy();
                onCleanup?.Invoke();
            };
        }

        void DestroyBanner(string advertiseId)
        {
            if (_banners.TryGetValue(advertiseId, out var old))
            {
                old.Destroy();
                _banners.Remove(advertiseId);
            }
        }

        void DestroyInterstitial(string advertiseId)
        {
            if (_interstitials.TryGetValue(advertiseId, out var old))
            {
                old.Destroy();
                _interstitials.Remove(advertiseId);
            }
        }

        void DestroyRewarded(string advertiseId)
        {
            if (_rewardedAds.TryGetValue(advertiseId, out var old))
            {
                old.Destroy();
                _rewardedAds.Remove(advertiseId);
            }
        }

        void DestroyAppOpen(string advertiseId)
        {
            if (_appOpenAds.TryGetValue(advertiseId, out var old))
            {
                old.Destroy();
                _appOpenAds.Remove(advertiseId);
            }
        }
    }
}
