using System;
using Devian.Domain.Game;

namespace Devian
{
    /// <summary>
    /// AdsManager.ShowAsync의 반환 결과.
    /// </summary>
    public readonly struct AdShowResult
    {
        public AdShowResult(
            string advertiseId,
            ADVERTISE_FORMAT format,
            AdProviderShowResult providerStatus)
        {
            AdvertiseId = advertiseId ?? string.Empty;
            Format = format;
            ProviderStatus = providerStatus;
        }

        public string AdvertiseId { get; }
        public ADVERTISE_FORMAT Format { get; }
        public AdProviderShowResult ProviderStatus { get; }
    }
}
