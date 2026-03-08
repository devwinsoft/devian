using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Devian.Domain.Common;
using Devian.Domain.Game;

namespace Devian
{
    /// <summary>
    /// 광고 오케스트레이터.
    /// TB_ADVERTISE 기반으로 placement를 해석하고, provider 초기화/로드/표시를 관리한다.
    /// Rewarded 성공 시 RewardManager로 지급을 위임한다.
    /// </summary>
    public sealed class AdManager : CompoSingleton<AdManager>
    {
        const string Tag = "AdManager";

        // ── State ──

        readonly Dictionary<ADVERTISE_PROVIDER, IAdProvider> _providers
            = new Dictionary<ADVERTISE_PROVIDER, IAdProvider>();

        readonly Dictionary<string, long> _lastShowTicks
            = new Dictionary<string, long>();

        string _currentShowCycleId;
        bool _rewardAppliedThisCycle;
        bool _initialized;

        /// <summary>
        /// SSV용 사용자 ID. InitializeAsync 전에 설정한다.
        /// 설정하지 않으면 SSV custom_data가 전송되지 않는다.
        /// </summary>
        public string UserId { get; set; }

        // ────────────────────────────────────────────
        // Lifecycle
        // ────────────────────────────────────────────

        protected override void onInitAwake()
        {
        }

        // ────────────────────────────────────────────
        // Public API
        // ────────────────────────────────────────────

        /// <summary>
        /// Provider 초기화 + AutoLoad placement preload. Idempotent.
        /// </summary>
        public async Task<CommonResult> InitializeAsync(CancellationToken ct = default)
        {
            if (_initialized)
                return CommonResult.Ok();

            try
            {
                // 고유 provider 타입 수집 → 초기화
                var providerTypes = new HashSet<ADVERTISE_PROVIDER>();
                var all = TB_ADVERTISE.GetAll();
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i].IsActive)
                        providerTypes.Add(all[i].Provider);
                }

                foreach (var pt in providerTypes)
                {
                    var provider = GetOrCreateProvider(pt);
                    var ok = await provider.InitializeAsync(ct);
                    if (!ok)
                        Debug.LogWarning($"[{Tag}] Provider init failed: {pt} (non-fatal)");
                }

                _initialized = true;

                // AutoLoad
                for (int i = 0; i < all.Count; i++)
                {
                    var row = all[i];
                    if (row.IsActive && row.AutoLoad)
                    {
                        // fire-and-forget preload — 실패해도 non-fatal
                        _ = PreloadAsync(row.AdvertiseId, ct);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Tag}] InitializeAsync exception (non-fatal): {ex.Message}");
            }

            return CommonResult.Ok();
        }

        /// <summary>
        /// 단일 placement preload.
        /// </summary>
        public async Task<CommonResult> PreloadAsync(string advertiseId, CancellationToken ct = default)
        {
            var row = TB_ADVERTISE.Get(advertiseId);
            if (row == null || !row.IsActive)
            {
                return CommonResult.Failure(
                    CommonErrorType.COMMON_INVALID_ARGUMENT,
                    $"ADVERTISE not found or inactive: {advertiseId}");
            }

            try
            {
                var provider = GetOrCreateProvider(row.Provider);
                var adUnitId = ResolveAdUnitId(row);
                var ok = await provider.LoadAsync(advertiseId, row.Format, adUnitId, ct);
                if (!ok)
                {
                    return CommonResult.Failure(
                        CommonErrorType.COMMON_UNKNOWN,
                        $"Ad load failed: {advertiseId}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Tag}] PreloadAsync exception: {ex.Message}");
                return CommonResult.Failure(CommonErrorType.COMMON_UNKNOWN, ex.Message);
            }

            return CommonResult.Ok();
        }

        /// <summary>
        /// 광고 표시 가능 여부. 동기 fast-gate (provider readiness는 미포함).
        /// </summary>
        public bool CanShow(string advertiseId)
        {
            var row = TB_ADVERTISE.Get(advertiseId);
            if (row == null || !row.IsActive)
                return false;

            // NoAds gating — REWARDED는 예외
            if (row.Format != ADVERTISE_FORMAT.REWARDED && IsNoAdsActive())
                return false;

            if (IsCooldownActive(advertiseId, row.CooldownSec))
                return false;

            return true;
        }

        /// <summary>
        /// 단일 광고 표시.
        /// Banner: 즉시 반환. 풀스크린: close까지 대기 후 반환.
        /// Rewarded 성공 시 RewardManager.ApplyRewardGroup 호출.
        /// skip=true이면 광고 노출 없이 Reward만 즉시 지급한다.
        /// </summary>
        public async Task<CommonResult<AdShowResult>> ShowAsync(
            string advertiseId,
            bool skip = false,
            CancellationToken ct = default)
        {
            // ── row 조회 ──
            var row = TB_ADVERTISE.Get(advertiseId);
            if (row == null || !row.IsActive)
            {
                return CommonResult<AdShowResult>.Failure(
                    CommonErrorType.COMMON_INVALID_ARGUMENT,
                    $"ADVERTISE not found or inactive: {advertiseId}");
            }

            // ── skip: 광고 없이 보상만 지급 ──
            if (skip)
            {
                return SkipAndReward(advertiseId, row);
            }

            // ── NoAds gating (REWARDED 제외) ──
            if (row.Format != ADVERTISE_FORMAT.REWARDED && IsNoAdsActive())
            {
                return CommonResult<AdShowResult>.Failure(
                    CommonErrorType.COMMON_INVALID_ARGUMENT,
                    "NoAds active — ad blocked");
            }

            // ── Cooldown ──
            if (IsCooldownActive(advertiseId, row.CooldownSec))
            {
                return CommonResult<AdShowResult>.Failure(
                    CommonErrorType.COMMON_INVALID_ARGUMENT,
                    $"Cooldown active: {advertiseId}");
            }

            var provider = GetOrCreateProvider(row.Provider);

            // ── Show cycle 시작 ──
            _currentShowCycleId = advertiseId;
            _rewardAppliedThisCycle = false;

            try
            {
                if (row.Format == ADVERTISE_FORMAT.BANNER)
                {
                    return await ShowBannerFlow(advertiseId, row, provider, ct);
                }
                else
                {
                    return await ShowFullScreenFlow(advertiseId, row, provider, ct);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Tag}] ShowAsync exception: {ex.Message}");
                return CommonResult<AdShowResult>.Failure(
                    CommonErrorType.COMMON_UNKNOWN, ex.Message);
            }
            finally
            {
                _currentShowCycleId = null;
            }
        }

        /// <summary>
        /// Banner hide. 동기.
        /// </summary>
        public void HideBanner(string advertiseId)
        {
            var row = TB_ADVERTISE.Get(advertiseId);
            if (row == null || row.Format != ADVERTISE_FORMAT.BANNER)
                return;

            if (_providers.TryGetValue(row.Provider, out var provider))
            {
                provider.Hide(advertiseId, ADVERTISE_FORMAT.BANNER);
            }
        }

        // ────────────────────────────────────────────
        // Show flows
        // ────────────────────────────────────────────

        async Task<CommonResult<AdShowResult>> ShowBannerFlow(
            string advertiseId,
            ADVERTISE row,
            IAdProvider provider,
            CancellationToken ct)
        {
            var showResult = await provider.ShowAsync(advertiseId, row.Format, ct);
            if (showResult != AdProviderShowResult.Shown)
            {
                return CommonResult<AdShowResult>.Failure(
                    CommonErrorType.COMMON_UNKNOWN,
                    $"Banner show failed: {advertiseId}");
            }

            RecordShowTime(advertiseId);

            return CommonResult<AdShowResult>.Success(new AdShowResult(
                advertiseId,
                row.Format,
                row.RewardGroupId,
                false,
                Array.Empty<RewardData>(),
                showResult));
        }

        async Task<CommonResult<AdShowResult>> ShowFullScreenFlow(
            string advertiseId,
            ADVERTISE row,
            IAdProvider provider,
            CancellationToken ct)
        {
            var closeTcs = new TaskCompletionSource<bool>();
            RewardData[] appliedRewards = Array.Empty<RewardData>();

            Action<string> onClosed = null;
            Action<string> onReward = null;

            onClosed = (id) =>
            {
                if (id == advertiseId)
                    closeTcs.TrySetResult(true);
            };

            onReward = (id) =>
            {
                if (id != advertiseId) return;
                if (_rewardAppliedThisCycle) return;
                if (row.Format != ADVERTISE_FORMAT.REWARDED) return;
                if (string.IsNullOrEmpty(row.RewardGroupId)) return;

                try
                {
                    var rewardResult = Singleton.Get<RewardManager>().ApplyRewardGroup(row.RewardGroupId);
                    if (rewardResult.IsSuccess)
                    {
                        _rewardAppliedThisCycle = true;
                        appliedRewards = rewardResult.Value.AppliedRewards;
                        Debug.Log($"[{Tag}] Reward applied: {row.RewardGroupId} ({appliedRewards.Length} items)");
                    }
                    else
                    {
                        Debug.LogWarning($"[{Tag}] Reward apply failed: {rewardResult.Error}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[{Tag}] RewardManager call failed: {ex.Message}");
                }
            };

            provider.OnClosed += onClosed;
            provider.OnRewardEarned += onReward;

            try
            {
                // SSV 설정 (REWARDED only)
                if (row.Format == ADVERTISE_FORMAT.REWARDED && !string.IsNullOrEmpty(UserId))
                {
                    var customData = $"{UserId}:{advertiseId}:{row.RewardGroupId}";
                    provider.SetRewardedSsvData(advertiseId, UserId, customData);
                }

                var showResult = await provider.ShowAsync(advertiseId, row.Format, ct);
                if (showResult != AdProviderShowResult.Shown)
                {
                    return CommonResult<AdShowResult>.Failure(
                        CommonErrorType.COMMON_UNKNOWN,
                        $"Ad show failed: {advertiseId}");
                }

                // CancellationToken 연동
                using (ct.Register(() => closeTcs.TrySetCanceled()))
                {
                    await closeTcs.Task;
                }

                RecordShowTime(advertiseId);

                return CommonResult<AdShowResult>.Success(new AdShowResult(
                    advertiseId,
                    row.Format,
                    row.RewardGroupId,
                    _rewardAppliedThisCycle,
                    appliedRewards,
                    AdProviderShowResult.Shown));
            }
            finally
            {
                provider.OnClosed -= onClosed;
                provider.OnRewardEarned -= onReward;
            }
        }

        // ────────────────────────────────────────────
        // Internal helpers
        // ────────────────────────────────────────────

        CommonResult<AdShowResult> SkipAndReward(string advertiseId, ADVERTISE row)
        {
            RewardData[] appliedRewards = Array.Empty<RewardData>();
            bool rewardApplied = false;

            if (row.Format == ADVERTISE_FORMAT.REWARDED
                && !string.IsNullOrEmpty(row.RewardGroupId))
            {
                try
                {
                    var rewardResult = Singleton.Get<RewardManager>()
                        .ApplyRewardGroup(row.RewardGroupId);
                    if (rewardResult.IsSuccess)
                    {
                        rewardApplied = true;
                        appliedRewards = rewardResult.Value.AppliedRewards;
                        Debug.Log($"[{Tag}] Skip reward applied: {row.RewardGroupId} ({appliedRewards.Length} items)");
                    }
                    else
                    {
                        Debug.LogWarning($"[{Tag}] Skip reward failed: {rewardResult.Error}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[{Tag}] Skip RewardManager call failed: {ex.Message}");
                }
            }

            return CommonResult<AdShowResult>.Success(new AdShowResult(
                advertiseId,
                row.Format,
                row.RewardGroupId,
                rewardApplied,
                appliedRewards,
                AdProviderShowResult.Skipped));
        }

        IAdProvider GetOrCreateProvider(ADVERTISE_PROVIDER providerType)
        {
            if (_providers.TryGetValue(providerType, out var cached))
                return cached;

            IAdProvider provider;
            switch (providerType)
            {
                case ADVERTISE_PROVIDER.GOOGLE_MOBILE_ADS:
                    provider = new GoogleMobileAdsProvider();
                    break;
                case ADVERTISE_PROVIDER.MOCK:
                default:
                    provider = new MockAdProvider();
                    break;
            }

            _providers[providerType] = provider;
            return provider;
        }

        static string ResolveAdUnitId(ADVERTISE row)
        {
#if UNITY_IOS
            return row.IosAdUnitId;
#elif UNITY_ANDROID
            return row.AndroidAdUnitId;
#else
            return row.AndroidAdUnitId; // Editor fallback
#endif
        }

        bool IsNoAdsActive()
        {
            if (!Singleton.TryGet<InventoryManager>(out var inv))
                return false;

            return inv.Storage.HasActiveRental("NO_ADS");
        }

        bool IsCooldownActive(string advertiseId, int cooldownSec)
        {
            if (cooldownSec <= 0)
                return false;

            if (!_lastShowTicks.TryGetValue(advertiseId, out var lastTicks))
                return false;

            var elapsed = DateTime.UtcNow.Ticks - lastTicks;
            return elapsed < TimeSpan.FromSeconds(cooldownSec).Ticks;
        }

        void RecordShowTime(string advertiseId)
        {
            _lastShowTicks[advertiseId] = DateTime.UtcNow.Ticks;
        }
    }
}
