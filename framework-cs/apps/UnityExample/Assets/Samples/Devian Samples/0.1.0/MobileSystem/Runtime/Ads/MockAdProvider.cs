using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Devian.Domain.Game;

namespace Devian
{
    /// <summary>
    /// 테스트용 Mock Ad Provider.
    /// Editor/CI 기본 provider. 외부 네트워크 요청 없이 scripted callback으로 동작한다.
    /// </summary>
    public sealed class MockAdProvider : IAdProvider
    {
        const string Tag = "MockAdProvider";
        const int MockLoadDelayMs = 100;
        const int MockShowDelayMs = 500;

        readonly HashSet<string> _loaded = new HashSet<string>();

        public event Action<string> OnLoaded;
        public event Action<string, string> OnFailedToLoad;
        public event Action<string> OnOpened;
        public event Action<string> OnClosed;
        public event Action<string> OnRewardEarned;

        /// <summary>시나리오 제어. 기본: Success.</summary>
        public MockAdScenario Scenario { get; set; } = MockAdScenario.Success;

        public async Task<bool> InitializeAsync(CancellationToken ct = default)
        {
            Debug.Log($"[{Tag}] InitializeAsync (mock)");
            await Task.Yield();
            return true;
        }

        public async Task<bool> LoadAsync(
            string advertiseId,
            ADVERTISE_FORMAT format,
            string adUnitId,
            CancellationToken ct = default)
        {
            Debug.Log($"[{Tag}] LoadAsync id={advertiseId} format={format} adUnit={adUnitId}");

            await Task.Delay(MockLoadDelayMs, ct);

            if (Scenario == MockAdScenario.LoadFail || Scenario == MockAdScenario.NoFill)
            {
                var reason = Scenario == MockAdScenario.NoFill ? "no fill" : "load fail";
                Debug.LogWarning($"[{Tag}] Load failed (mock): {reason}");
                OnFailedToLoad?.Invoke(advertiseId, $"Mock: {reason}");
                return false;
            }

            _loaded.Add(advertiseId);
            OnLoaded?.Invoke(advertiseId);
            return true;
        }

        public async Task<AdProviderShowResult> ShowAsync(
            string advertiseId,
            ADVERTISE_FORMAT format,
            CancellationToken ct = default)
        {
            if (!_loaded.Contains(advertiseId))
            {
                Debug.LogWarning($"[{Tag}] Show: not loaded id={advertiseId}");
                return AdProviderShowResult.NotReady;
            }

            if (Scenario == MockAdScenario.ShowFail)
            {
                Debug.LogWarning($"[{Tag}] Show failed (mock)");
                _loaded.Remove(advertiseId);
                return AdProviderShowResult.Failed;
            }

            await Task.Delay(MockShowDelayMs, ct);

            OnOpened?.Invoke(advertiseId);

            // REWARDED: 시나리오가 CloseWithoutReward가 아니면 reward 콜백 발화
            if (format == ADVERTISE_FORMAT.REWARDED
                && Scenario != MockAdScenario.CloseWithoutReward)
            {
                OnRewardEarned?.Invoke(advertiseId);
            }

            // Banner는 지속형이므로 loaded 유지, 풀스크린은 consumed
            if (format != ADVERTISE_FORMAT.BANNER)
            {
                _loaded.Remove(advertiseId);
            }

            OnClosed?.Invoke(advertiseId);
            return AdProviderShowResult.Shown;
        }

        public void Hide(string advertiseId, ADVERTISE_FORMAT format)
        {
            _loaded.Remove(advertiseId);
            Debug.Log($"[{Tag}] Hide id={advertiseId}");
        }

        public void SetRewardedSsvData(string advertiseId, string userId, string customData)
        {
            // Mock: SSV 미지원. 무시.
        }
    }

    /// <summary>Mock provider 시나리오.</summary>
    public enum MockAdScenario
    {
        Success,
        LoadFail,
        ShowFail,
        NoFill,
        CloseWithoutReward,
    }
}
