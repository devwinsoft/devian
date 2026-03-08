// SSOT: skills/devian-unity/20-domain-common-system/33-time-manager/SKILL.md

using System;

namespace Devian
{
    /// <summary>
    /// Simulated server time source anchored by InitServerTime.
    /// </summary>
    public sealed class TimeManager : CompoSingleton<TimeManager>
    {
        private const long MinUnixTimeMs = -62135596800000L; // 0001-01-01T00:00:00.000Z
        private const long MaxUnixTimeMs = 253402300799999L; // 9999-12-31T23:59:59.999Z

        private long _serverAnchorUtcMs;
        private long _clientAnchorUtcMs;
        private bool _isInitialized;

        public bool IsInitialized => _isInitialized;

        public long serverNowUtcMs
        {
            get
            {
                if (!_isInitialized)
                    return 0L;

                return calculateServerNowUtcMs();
            }
        }

        public DateTime serverNowUtcDate
        {
            get
            {
                if (!_isInitialized)
                    return DateTime.MinValue;

                return ToUtcDateTime(serverNowUtcMs);
            }
        }

        public void InitServerTime(long serverNowUtcMs)
        {
            if (serverNowUtcMs <= 0L)
            {
                _serverAnchorUtcMs = 0L;
                _clientAnchorUtcMs = 0L;
                _isInitialized = false;
                return;
            }

            _serverAnchorUtcMs = ClampUnixTimeMs(serverNowUtcMs);
            _clientAnchorUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _isInitialized = true;
        }

        public bool TryGetServerNowUtcMs(out long value)
        {
            if (!_isInitialized)
            {
                value = 0L;
                return false;
            }

            value = calculateServerNowUtcMs();
            return true;
        }

        public bool TryGetServerNowUtcDate(out DateTime value)
        {
            if (!_isInitialized)
            {
                value = DateTime.MinValue;
                return false;
            }

            value = ToUtcDateTime(calculateServerNowUtcMs());
            return true;
        }

        private long calculateServerNowUtcMs()
        {
            var clientNowUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var elapsedMs = Math.Max(0L, clientNowUtcMs - _clientAnchorUtcMs);
            return ClampUnixTimeMs(_serverAnchorUtcMs + elapsedMs);
        }

        private static DateTime ToUtcDateTime(long utcTimeMs)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(ClampUnixTimeMs(utcTimeMs)).UtcDateTime;
        }

        private static long ClampUnixTimeMs(long value)
        {
            if (value < MinUnixTimeMs)
                return MinUnixTimeMs;

            if (value > MaxUnixTimeMs)
                return MaxUnixTimeMs;

            return value;
        }
    }
}
