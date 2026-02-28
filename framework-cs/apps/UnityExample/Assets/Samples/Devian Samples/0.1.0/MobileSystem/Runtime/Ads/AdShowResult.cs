using System;
using Devian.Domain.Game;

namespace Devian
{
    /// <summary>
    /// AdManager.ShowAsync의 반환 결과.
    /// </summary>
    public readonly struct AdShowResult
    {
        public AdShowResult(
            string advertiseId,
            ADVERTISE_FORMAT format,
            string rewardGroupId,
            bool rewardApplied,
            RewardData[] appliedRewards,
            AdProviderShowResult providerStatus)
        {
            AdvertiseId = advertiseId ?? string.Empty;
            Format = format;
            RewardGroupId = rewardGroupId ?? string.Empty;
            RewardApplied = rewardApplied;
            AppliedRewards = appliedRewards ?? Array.Empty<RewardData>();
            ProviderStatus = providerStatus;
        }

        public string AdvertiseId { get; }
        public ADVERTISE_FORMAT Format { get; }
        public string RewardGroupId { get; }
        public bool RewardApplied { get; }
        public RewardData[] AppliedRewards { get; }
        public AdProviderShowResult ProviderStatus { get; }
    }
}
