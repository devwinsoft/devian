using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Game;
using UnityEngine;
using UnityEngine.Networking;

namespace Devian
{
    public sealed class RemoteDataManager : CompoSingleton<RemoteDataManager>
    {
        const string Tag = nameof(RemoteDataManager);
        const string ServerTimeHeadUrl = "https://worldtimeapi.org";

        long _serverNowUtcMsAtSync;
        long _clientNowUtcMsAtServerSync;

        public VersionNumber CurrentVersionNumber { get; private set; }
        public VersionNumber MinVersionNumber { get; private set; }

        public static long ServerNowUtcMs
        {
            get
            {
                if (TryGet(out var mgr) && mgr != null
                    && mgr._serverNowUtcMsAtSync > 0L && mgr._clientNowUtcMsAtServerSync > 0L)
                {
                    var elapsed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - mgr._clientNowUtcMsAtServerSync;
                    if (elapsed < 0L) elapsed = 0L;
                    return mgr._serverNowUtcMsAtSync + elapsed;
                }
                return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }
        }

        public static bool TryGetServerNowUtcMs(out long serverNowUtcMs)
        {
            if (TryGet(out var mgr) && mgr != null
                && mgr._serverNowUtcMsAtSync > 0L && mgr._clientNowUtcMsAtServerSync > 0L)
            {
                var elapsed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - mgr._clientNowUtcMsAtServerSync;
                if (elapsed < 0L) elapsed = 0L;
                serverNowUtcMs = mgr._serverNowUtcMsAtSync + elapsed;
                return true;
            }

            serverNowUtcMs = 0L;
            return false;
        }

        protected override void onInitAwake()
        {
            var nowUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            setServerNowSnapshot(nowUtcMs);
        }

        public async Task<GameResult<VersionCheckResult>> InitializeAsync(
            VersionNumber clientVersion,
            CancellationToken ct = default)
        {
            var versionCheck = await VersionCheckAsync(clientVersion, ct);
            if (versionCheck.IsFailure)
                return GameResult<VersionCheckResult>.Failure(versionCheck.Error!);

            var syncServerUtc = await SyncServerUtcAsync(ct);
            if (syncServerUtc.IsFailure)
            {
                Debug.LogWarning(
                    $"[{Tag}] Server UTC sync failed during initialize (non-fatal). " +
                    $"code={syncServerUtc.Error.Code}, message={syncServerUtc.Error.Message}");
            }

            return versionCheck;
        }

        public async Task<GameResult<VersionCheckResult>> VersionCheckAsync(
            VersionNumber clientVersion,
            CancellationToken ct = default)
        {
#if UNITY_EDITOR
            CurrentVersionNumber = clientVersion;
            MinVersionNumber = clientVersion;
            _ = ct;
            return GameResult<VersionCheckResult>.Success(VersionCheckResult.Success);
#else
            var config = await fetchVersionCheckConfigAsync(ct);
            if (config.IsFailure)
                return GameResult<VersionCheckResult>.Failure(config.Error!);

            var result = evaluateVersionCheck(
                config.Value,
                clientVersion,
                out var currentVersion,
                out var minVersion);

            CurrentVersionNumber = currentVersion;
            MinVersionNumber = minVersion;
            return GameResult<VersionCheckResult>.Success(result);
#endif
        }

        public async Task<GameResult> SyncServerUtcAsync(CancellationToken ct = default)
        {
#if UNITY_EDITOR
            _ = ct;
            var nowUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            setServerNowSnapshot(nowUtcMs);
            return GameResult.Ok();
#else
            var fetch = await fetchServerNowUtcAsync(ct);
            if (fetch.IsFailure)
            {
                setServerNowSnapshot(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                return GameResult.Failure(fetch.Error!);
            }

            setServerNowSnapshot(fetch.Value);
            return GameResult.Ok();
#endif
        }

        async Task<GameResult<VersionCheckConfig>> fetchVersionCheckConfigAsync(CancellationToken ct)
        {
            if (!tryResolveVersionCheckUrl(out var url))
            {
                return GameResult<VersionCheckConfig>.Failure(
                    GAME_ERROR_TYPE.VERSION_CHECK_URL_NOT_CONFIGURED,
                    "Version check URL is not configured for this platform.");
            }

            using var request = UnityWebRequest.Get(url);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 5;
            var operation = request.SendWebRequest();

            while (!operation.isDone)
            {
                if (ct.IsCancellationRequested)
                {
                    request.Abort();
                    ct.ThrowIfCancellationRequested();
                }

                await Task.Yield();
            }

            ct.ThrowIfCancellationRequested();

            if (request.result != UnityWebRequest.Result.Success)
            {
                return GameResult<VersionCheckConfig>.Failure(
                    GAME_ERROR_TYPE.VERSION_CHECK_NETWORK_FAILED,
                    $"Version config request failed: {request.error}");
            }

            var json = request.downloadHandler?.text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                return GameResult<VersionCheckConfig>.Failure(
                    GAME_ERROR_TYPE.VERSION_CHECK_RESPONSE_EMPTY,
                    "Version config response is empty.");
            }

            try
            {
                var config = JsonUtility.FromJson<VersionCheckConfig>(json);
                if (config == null)
                {
                    return GameResult<VersionCheckConfig>.Failure(
                        GAME_ERROR_TYPE.VERSION_CHECK_PARSE_FAILED,
                        "Version config JSON parse failed.");
                }

                return GameResult<VersionCheckConfig>.Success(config);
            }
            catch (Exception ex)
            {
                return GameResult<VersionCheckConfig>.Failure(
                    GAME_ERROR_TYPE.VERSION_CHECK_PARSE_FAILED,
                    $"Version config JSON parse failed: {ex.Message}");
            }
        }

        static bool tryResolveVersionCheckUrl(out string url)
        {
            url = string.Empty;

            var app = MobileApplication.Instance;
            if (app == null)
                return false;

#if UNITY_ANDROID
            url = app.VersionCheckAOS;
#elif UNITY_IOS
            url = app.VersionCheckIOS;
#else
            url = string.Empty;
#endif

            url = string.IsNullOrWhiteSpace(url) ? string.Empty : url.Trim();
            return !string.IsNullOrEmpty(url);
        }

        static async Task<GameResult<long>> fetchServerNowUtcAsync(CancellationToken ct)
        {
            using var request = UnityWebRequest.Head(ServerTimeHeadUrl);
            request.timeout = 5;
            var operation = request.SendWebRequest();

            while (!operation.isDone)
            {
                if (ct.IsCancellationRequested)
                {
                    request.Abort();
                    ct.ThrowIfCancellationRequested();
                }

                await Task.Yield();
            }

            ct.ThrowIfCancellationRequested();

            if (request.result != UnityWebRequest.Result.Success)
            {
                return GameResult<long>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"Server time request failed: {request.error}");
            }

            var dateHeader = request.GetResponseHeader("Date");
            if (string.IsNullOrWhiteSpace(dateHeader))
                dateHeader = request.GetResponseHeader("date");
            if (string.IsNullOrWhiteSpace(dateHeader))
            {
                return GameResult<long>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "Server time response does not contain Date header.");
            }

            if (!DateTimeOffset.TryParse(
                    dateHeader,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var serverUtc))
            {
                return GameResult<long>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"Server time Date header parse failed: {dateHeader}");
            }

            var serverNowUtcMs = serverUtc.ToUnixTimeMilliseconds();
            if (serverNowUtcMs <= 0L)
            {
                return GameResult<long>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "Server time is invalid.");
            }

            return GameResult<long>.Success(serverNowUtcMs);
        }

        static VersionCheckResult evaluateVersionCheck(
            VersionCheckConfig config,
            VersionNumber clientVersion,
            out VersionNumber currentVersion,
            out VersionNumber minVersion)
        {
            currentVersion = default;
            minVersion = default;

            if (config == null)
                return VersionCheckResult.Success;

            var minVersionText = string.IsNullOrWhiteSpace(config.minVersion)
                ? string.Empty
                : config.minVersion.Trim();
            var currentVersionText = string.IsNullOrWhiteSpace(config.currentVersion)
                ? string.Empty
                : config.currentVersion.Trim();

            var parsedMinVersion = default(VersionNumber);
            var hasMinVersion = !string.IsNullOrEmpty(minVersionText)
                && VersionNumber.TryParse(minVersionText, out parsedMinVersion);
            if (hasMinVersion)
                minVersion = parsedMinVersion;

            var parsedCurrentVersion = default(VersionNumber);
            var hasCurrentVersion = !string.IsNullOrEmpty(currentVersionText)
                && VersionNumber.TryParse(currentVersionText, out parsedCurrentVersion);
            if (hasCurrentVersion)
                currentVersion = parsedCurrentVersion;

            if (hasMinVersion && clientVersion < parsedMinVersion)
                return VersionCheckResult.ForceUpdate;

            if (hasCurrentVersion && clientVersion < parsedCurrentVersion)
                return VersionCheckResult.RecommendUpdate;

            return VersionCheckResult.Success;
        }

        void setServerNowSnapshot(long serverNowUtcMs)
        {
            if (serverNowUtcMs <= 0L)
                return;

            _serverNowUtcMsAtSync = serverNowUtcMs;
            _clientNowUtcMsAtServerSync = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}
