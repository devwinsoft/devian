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
    /// 광고 표시만 담당하며 보상 지급은 호출자가 처리한다.
    /// </summary>
    public sealed class AdsManager : CompoSingleton<AdsManager>
    {
        const string Tag = "AdsManager";
        const string DefaultAdvertiseIdValue = "advertise_001";

        // ── State ──

        readonly Dictionary<ADVERTISE_PROVIDER, IAdProvider> _providers
            = new Dictionary<ADVERTISE_PROVIDER, IAdProvider>();

        readonly Dictionary<string, long> _lastShowTicks
            = new Dictionary<string, long>();

        string _defaultAdvertiseId = DefaultAdvertiseIdValue;
        bool _initialized;

        /// <summary>
        /// SSV용 사용자 ID. InitializeAsync 전에 설정한다.
        /// 설정하지 않으면 SSV custom_data가 전송되지 않는다.
        /// </summary>
        public string UserId { get; set; }

        // ────────────────────────────────────────────
        // Public API
        // ────────────────────────────────────────────

        /// <summary>
        /// Provider 초기화 + AutoLoad placement preload. Idempotent.
        /// </summary>
        public async Task<GameResult> InitializeAsync(CancellationToken ct = default)
        {
            if (_initialized)
                return GameResult.Ok();

            try
            {
                // 고유 provider 타입 수집 → 초기화
                var providerTypes = new HashSet<ADVERTISE_PROVIDER>();
                var all = TB_ADVERTISE.GetAll();
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i].is_active)
                        providerTypes.Add(all[i].provider);
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
                    if (row.is_active && row.auto_load)
                    {
                        // fire-and-forget preload — 실패해도 non-fatal
                        _ = PreloadAsync(row.advertise_id, ct);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Tag}] InitializeAsync exception (non-fatal): {ex.Message}");
            }

            return GameResult.Ok();
        }

        /// <summary>
        /// 단일 placement preload.
        /// </summary>
        public async Task<GameResult> PreloadAsync(string advertiseId, CancellationToken ct = default)
        {
            var row = TB_ADVERTISE.Get(advertiseId);
            if (row == null || !row.is_active)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.ADS_PLACEMENT_NOT_FOUND,
                    $"ADVERTISE not found or inactive: {advertiseId}");
            }

            try
            {
                var provider = GetOrCreateProvider(row.provider);
                var adUnitId = ResolveAdUnitId(row);
                var ok = await provider.LoadAsync(advertiseId, row.format, adUnitId, ct);
                if (!ok)
                {
                    return GameResult.Failure(
                        GAME_ERROR_TYPE.ADS_LOAD_FAILED,
                        $"Ad load failed: {advertiseId}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Tag}] PreloadAsync exception: {ex.Message}");
                return GameResult.Failure(GAME_ERROR_TYPE.ADS_LOAD_FAILED, ex.Message);
            }

            return GameResult.Ok();
        }

        public void SetDefaultId(string advertiseId)
        {
            _defaultAdvertiseId = string.IsNullOrWhiteSpace(advertiseId)
                ? DefaultAdvertiseIdValue
                : advertiseId.Trim();
        }

        public Task<GameResult<AdShowResult>> ShowAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_defaultAdvertiseId))
            {
                return Task.FromResult(
                    GameResult<AdShowResult>.Failure(
                        GAME_ERROR_TYPE.ADS_PLACEMENT_NOT_FOUND,
                        "Default advertise id is empty."));
            }

            return ShowAsync(_defaultAdvertiseId, ct);
        }

        /// <summary>
        /// 기본 advertiseId 광고 표시 가능 여부.
        /// </summary>
        public bool CanShow()
        {
            return CanShow(_defaultAdvertiseId);
        }

        /// <summary>
        /// 광고 표시 가능 여부. 동기 fast-gate (provider readiness는 미포함).
        /// </summary>
        public bool CanShow(string advertiseId)
        {
            var row = TB_ADVERTISE.Get(advertiseId);
            if (row == null || !row.is_active)
                return false;

            // NoAds 활성 → REWARDED 제외, 광고 없이 즉시 성공 (ShowAsync에서 Skipped 반환)
            if (row.format != ADVERTISE_FORMAT.REWARDED && IsNoAdsActive())
                return true;

            if (IsCooldownActive(advertiseId, row.cooldown_sec))
                return false;

            return true;
        }

        /// <summary>
        /// 단일 광고 표시.
        /// Banner: 즉시 반환. 풀스크린: close까지 대기 후 반환.
        /// </summary>
        public async Task<GameResult<AdShowResult>> ShowAsync(string advertiseId, CancellationToken ct = default)
        {
            // ── row 조회 ──
            var row = TB_ADVERTISE.Get(advertiseId);
            if (row == null || !row.is_active)
            {
                return GameResult<AdShowResult>.Failure(
                    GAME_ERROR_TYPE.ADS_PLACEMENT_NOT_FOUND,
                    $"ADVERTISE not found or inactive: {advertiseId}");
            }

            // ── NoAds 활성 → REWARDED 제외, 광고 없이 즉시 성공 ──
            if (row.format != ADVERTISE_FORMAT.REWARDED && IsNoAdsActive())
            {
                return GameResult<AdShowResult>.Success(new AdShowResult(
                    advertiseId,
                    row.format,
                    AdProviderShowResult.Skipped));
            }

            // ── Cooldown ──
            if (IsCooldownActive(advertiseId, row.cooldown_sec))
            {
                return GameResult<AdShowResult>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"Cooldown active: {advertiseId}");
            }

            var provider = GetOrCreateProvider(row.provider);

            try
            {
                if (row.format == ADVERTISE_FORMAT.BANNER)
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
                return GameResult<AdShowResult>.Failure(
                    GAME_ERROR_TYPE.ADS_SHOW_FAILED, ex.Message);
            }
        }

        /// <summary>
        /// Banner hide. 동기.
        /// </summary>
        public void HideBanner(string advertiseId)
        {
            var row = TB_ADVERTISE.Get(advertiseId);
            if (row == null || row.format != ADVERTISE_FORMAT.BANNER)
                return;

            if (_providers.TryGetValue(row.provider, out var provider))
            {
                provider.Hide(advertiseId, ADVERTISE_FORMAT.BANNER);
            }
        }

        // ────────────────────────────────────────────
        // Show flows
        // ────────────────────────────────────────────

        async Task<GameResult<AdShowResult>> ShowBannerFlow(
            string advertiseId,
            ADVERTISE row,
            IAdProvider provider,
            CancellationToken ct)
        {
            var showResult = await provider.ShowAsync(advertiseId, row.format, ct);
            if (showResult != AdProviderShowResult.Shown)
            {
                return GameResult<AdShowResult>.Failure(
                    GAME_ERROR_TYPE.ADS_SHOW_FAILED,
                    $"Banner show failed: {advertiseId}");
            }

            RecordShowTime(advertiseId);

            return GameResult<AdShowResult>.Success(new AdShowResult(
                advertiseId,
                row.format,
                showResult));
        }

        async Task<GameResult<AdShowResult>> ShowFullScreenFlow(
            string advertiseId,
            ADVERTISE row,
            IAdProvider provider,
            CancellationToken ct)
        {
            var closeTcs = new TaskCompletionSource<bool>();

            Action<string> onClosed = null;

            onClosed = (id) =>
            {
                if (id == advertiseId)
                    closeTcs.TrySetResult(true);
            };

            provider.OnClosed += onClosed;

            try
            {
                // SSV 설정 (REWARDED only)
                if (row.format == ADVERTISE_FORMAT.REWARDED && !string.IsNullOrEmpty(UserId))
                {
                    var customData = $"{UserId}:{advertiseId}";
                    provider.SetRewardedSsvData(advertiseId, UserId, customData);
                }

                var showResult = await provider.ShowAsync(advertiseId, row.format, ct);
                if (showResult != AdProviderShowResult.Shown)
                {
                    return GameResult<AdShowResult>.Failure(
                        GAME_ERROR_TYPE.ADS_SHOW_FAILED,
                        $"Ad show failed: {advertiseId}");
                }

                // CancellationToken 연동
                using (ct.Register(() => closeTcs.TrySetCanceled()))
                {
                    await closeTcs.Task;
                }

                RecordShowTime(advertiseId);

                return GameResult<AdShowResult>.Success(new AdShowResult(
                    advertiseId,
                    row.format,
                    AdProviderShowResult.Shown));
            }
            finally
            {
                provider.OnClosed -= onClosed;
            }
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
            return row.ios_ad_unit_id;
#elif UNITY_ANDROID
            return row.android_ad_unit_id;
#else
            return row.android_ad_unit_id; // Editor fallback
#endif
        }

        bool IsNoAdsActive()
        {
            if (!Singleton.TryGet<InventoryManager>(out var inv))
                return false;

            return inv.HasActiveRental("NO_ADS");
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
