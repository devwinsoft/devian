using System;
using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Common;

namespace Devian
{
    public sealed class RemoteConfigManager : CompoSingleton<RemoteConfigManager>
    {
        private const long MinUnixTimeMs = -62135596800000L; // 0001-01-01T00:00:00.000Z
        private const long MaxUnixTimeMs = 253402300799999L; // 9999-12-31T23:59:59.999Z

        readonly RemoteConfigStorage _storage = new();
        bool _initialized;

        public RemoteConfigStorage Storage => _storage;
        public bool IsInitialized => _initialized;

        public long serverNowUtcMs
        {
            get
            {
                if (!TryGetServerNowUtcMs(out var value))
                    return 0L;
                return value;
            }
        }

        public DateTime serverNowUtcDate
        {
            get
            {
                if (!TryGetServerNowUtcDate(out var value))
                    return DateTime.MinValue;
                return value;
            }
        }

        protected override void onInitAwake()
        {
        }

        public async Task<CommonResult> InitializeAsync(
            RemoteConfigSnapshot preloadedSnapshot = null,
            CancellationToken ct = default)
        {
            var refresh = preloadedSnapshot != null
                ? applySnapshot(preloadedSnapshot)
                : await RefreshAsync(ct);
            if (refresh.IsFailure)
                return CommonResult.Failure(refresh.Error!);

            _initialized = true;
            return CommonResult.Ok();
        }

        public async Task<CommonResult<RemoteConfigSnapshot>> RefreshAsync(CancellationToken ct = default)
        {
            var remoteConfig = await getRemoteConfigAsync(ct);
            if (remoteConfig.IsFailure)
                return remoteConfig;

            return applySnapshot(remoteConfig.Value);
        }

        public bool TryGetServerNowUtcMs(out long value)
        {
            value = 0L;
            var snapshot = _storage.snapshot;
            if (snapshot == null || snapshot.serverNowUtcMs <= 0L)
                return false;

            var clientNowUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var elapsedMs = Math.Max(0L, clientNowUtcMs - _storage.snapshotReceivedAtClientUtcMs);
            value = ClampUnixTimeMs(snapshot.serverNowUtcMs + elapsedMs);
            return true;
        }

        public bool TryGetServerNowUtcDate(out DateTime value)
        {
            if (!TryGetServerNowUtcMs(out var utcMs))
            {
                value = DateTime.MinValue;
                return false;
            }

            value = DateTimeOffset.FromUnixTimeMilliseconds(ClampUnixTimeMs(utcMs)).UtcDateTime;
            return true;
        }

        public void ClearStorage()
        {
            _storage.Clear();
            _initialized = false;
        }

        Task<CommonResult<RemoteConfigSnapshot>> getRemoteConfigAsync(CancellationToken ct)
        {
#if UNITY_EDITOR
            return Task.FromResult(CommonResult<RemoteConfigSnapshot>.Success(
                new RemoteConfigSnapshot(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())));
#else
            return FirebaseManager.Instance.GetRemoteConfigAsync(ct);
#endif
        }

        CommonResult<RemoteConfigSnapshot> applySnapshot(RemoteConfigSnapshot snapshot)
        {
            snapshot ??= new RemoteConfigSnapshot();
            _storage.snapshot = snapshot;
            _storage.snapshotReceivedAtClientUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return CommonResult<RemoteConfigSnapshot>.Success(snapshot);
        }

        static long ClampUnixTimeMs(long value)
        {
            if (value < MinUnixTimeMs)
                return MinUnixTimeMs;

            if (value > MaxUnixTimeMs)
                return MaxUnixTimeMs;

            return value;
        }
    }
}
