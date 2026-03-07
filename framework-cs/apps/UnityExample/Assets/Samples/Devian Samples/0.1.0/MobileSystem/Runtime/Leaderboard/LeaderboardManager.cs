using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Common;
using UnityEngine;
using UnityEngine.SocialPlatforms;

namespace Devian
{
    /// <summary>
    /// Leaderboard facade.
    /// Public API only accepts internal leaderboard IDs.
    /// </summary>
    public sealed class LeaderboardManager : CompoSingleton<LeaderboardManager>
    {
        private const string Tag = nameof(LeaderboardManager);

        [Serializable]
        private enum LeaderboardScoreOrder
        {
            HighBetter = 0,
            LowBetter = 1,
        }

        private enum RuntimePlatformKind
        {
            Unsupported = 0,
            Apple = 1,
            Google = 2,
        }

        [Serializable]
        private sealed class LeaderboardMapEntry
        {
            public string leaderboardId = string.Empty;
            public bool isActive = true;
            public string appleLeaderboardId = string.Empty;
            public string googleLeaderboardId = string.Empty;
            public LeaderboardScoreOrder scoreOrder = LeaderboardScoreOrder.HighBetter;

            public string InternalId => (leaderboardId ?? string.Empty).Trim();

            public string ResolvePlatformId(RuntimePlatformKind platform)
            {
                switch (platform)
                {
                    case RuntimePlatformKind.Apple:
                        return (appleLeaderboardId ?? string.Empty).Trim();
                    case RuntimePlatformKind.Google:
                        return (googleLeaderboardId ?? string.Empty).Trim();
                    default:
                        return string.Empty;
                }
            }
        }

        private interface ILeaderboardPlatformAdapter
        {
            Task<CommonResult> InitializeAsync(CancellationToken ct);
            Task<CommonResult> ReportScoreAsync(string platformLeaderboardId, long score, CancellationToken ct);
        }

        [SerializeField] private List<LeaderboardMapEntry> leaderboardMappings = new List<LeaderboardMapEntry>();

        private readonly Dictionary<string, LeaderboardMapEntry> _leaderboardById
            = new Dictionary<string, LeaderboardMapEntry>(StringComparer.Ordinal);

        private readonly SemaphoreSlim _initializeGate = new SemaphoreSlim(1, 1);

        private ILeaderboardPlatformAdapter _adapter;
        private bool _initialized;

        protected override void Awake()
        {
            base.Awake();
            rebuildMappingCaches();
        }

        public async Task<CommonResult> InitializeAsync(CancellationToken ct = default)
        {
            await _initializeGate.WaitAsync(ct);
            try
            {
                if (_initialized)
                    return CommonResult.Ok();

                rebuildMappingCaches();
                _adapter = _adapter ?? createAdapter(getRuntimePlatform());

                var init = await _adapter.InitializeAsync(ct);
                if (init.IsFailure)
                    return CommonResult.Failure(init.Error!);

                _initialized = true;
                return CommonResult.Ok();
            }
            finally
            {
                _initializeGate.Release();
            }
        }

        public async Task<CommonResult> ReportScoreAsync(string leaderboardId, long score, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var guard = ensureInitialized();
            if (guard.IsFailure)
                return guard;

            if (score < 0)
            {
                return CommonResult.Failure(
                    CommonErrorType.COMMON_INVALID_ARGUMENT,
                    $"Invalid leaderboard score: {score}");
            }

            var resolve = tryResolveLeaderboardPlatformId(leaderboardId, out var platformLeaderboardId);
            if (resolve.IsFailure)
                return resolve;

            return await _adapter.ReportScoreAsync(platformLeaderboardId, score, ct);
        }

        private CommonResult ensureInitialized()
        {
            if (_initialized)
                return CommonResult.Ok();

            return CommonResult.Failure(
                CommonErrorType.COMMON_INVALID_ARGUMENT,
                "LeaderboardManager.InitializeAsync must be called before API use.");
        }

        private CommonResult tryResolveLeaderboardPlatformId(string leaderboardId, out string platformLeaderboardId)
        {
            platformLeaderboardId = string.Empty;

            if (string.IsNullOrWhiteSpace(leaderboardId))
            {
                return CommonResult.Failure(
                    CommonErrorType.COMMON_INVALID_ARGUMENT,
                    "leaderboardId is empty.");
            }

            if (!_leaderboardById.TryGetValue(leaderboardId.Trim(), out var entry) || entry == null || !entry.isActive)
            {
                return CommonResult.Failure(
                    CommonErrorType.COMMON_INVALID_ARGUMENT,
                    $"Active leaderboard mapping not found: {leaderboardId}");
            }

            platformLeaderboardId = entry.ResolvePlatformId(getRuntimePlatform());
            if (string.IsNullOrEmpty(platformLeaderboardId))
            {
                return CommonResult.Failure(
                    CommonErrorType.COMMON_INVALID_ARGUMENT,
                    $"Platform leaderboard ID mapping missing: {leaderboardId}");
            }

            return CommonResult.Ok();
        }

        private void rebuildMappingCaches()
        {
            _leaderboardById.Clear();
            for (var i = 0; i < leaderboardMappings.Count; i++)
            {
                var row = leaderboardMappings[i];
                if (row == null)
                    continue;

                var id = row.InternalId;
                if (string.IsNullOrEmpty(id))
                    continue;

                if (_leaderboardById.ContainsKey(id))
                    Debug.LogWarning($"[{Tag}] Duplicate leaderboardId mapping. override id={id}");

                _leaderboardById[id] = row;
            }
        }

        private static RuntimePlatformKind getRuntimePlatform()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return RuntimePlatformKind.Apple;
#elif UNITY_ANDROID && !UNITY_EDITOR
            return RuntimePlatformKind.Google;
#else
            return RuntimePlatformKind.Unsupported;
#endif
        }

        private static ILeaderboardPlatformAdapter createAdapter(RuntimePlatformKind platform)
        {
            switch (platform)
            {
                case RuntimePlatformKind.Apple:
                    return new AppleLeaderboardPlatformAdapter();
                case RuntimePlatformKind.Google:
                    return new GoogleLeaderboardPlatformAdapter();
                default:
                    return new UnsupportedLeaderboardPlatformAdapter();
            }
        }

        private sealed class UnsupportedLeaderboardPlatformAdapter : ILeaderboardPlatformAdapter
        {
            public Task<CommonResult> InitializeAsync(CancellationToken ct)
                => Task.FromResult(CommonResult.Failure(
                    CommonErrorType.LOGIN_UNSUPPORTED,
                    "Leaderboard is not supported on this platform."));

            public Task<CommonResult> ReportScoreAsync(string platformLeaderboardId, long score, CancellationToken ct)
                => Task.FromResult(CommonResult.Failure(
                    CommonErrorType.LOGIN_UNSUPPORTED,
                    "Leaderboard is not supported on this platform."));
        }

        private sealed class AppleLeaderboardPlatformAdapter : ILeaderboardPlatformAdapter
        {
            public Task<CommonResult> InitializeAsync(CancellationToken ct)
            {
#if UNITY_IOS && !UNITY_EDITOR
                return Task.FromResult(CommonResult.Ok());
#else
                return Task.FromResult(CommonResult.Failure(
                    CommonErrorType.LOGIN_UNSUPPORTED,
                    "Game Center adapter is not available on this platform."));
#endif
            }

            public async Task<CommonResult> ReportScoreAsync(string platformLeaderboardId, long score, CancellationToken ct)
            {
#if UNITY_IOS && !UNITY_EDITOR
                var auth = ensureAuthenticated();
                if (auth.IsFailure)
                    return auth;

                try
                {
                    var tcs = new TaskCompletionSource<bool>();
                    Social.ReportScore(score, platformLeaderboardId, success => tcs.TrySetResult(success));
                    var success = await tcs.Task;

                    return success
                        ? CommonResult.Ok()
                        : CommonResult.Failure(CommonErrorType.COMMON_SERVER, "Game Center score report failed.");
                }
                catch (Exception ex)
                {
                    return CommonResult.Failure(CommonErrorType.COMMON_SERVER, ex.Message);
                }
#else
                return CommonResult.Failure(CommonErrorType.LOGIN_UNSUPPORTED, "Game Center adapter is not available on this platform.");
#endif
            }

            private static CommonResult ensureAuthenticated()
            {
                if (Social.localUser != null && Social.localUser.authenticated)
                    return CommonResult.Ok();

                return CommonResult.Failure(CommonErrorType.COMMON_AUTH, "Game Center authentication required.");
            }
        }

        private sealed class GoogleLeaderboardPlatformAdapter : ILeaderboardPlatformAdapter
        {
            private bool _resolved;
            private object _platformInstance;
            private MethodInfo _reportScoreMethod;

            public Task<CommonResult> InitializeAsync(CancellationToken ct)
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (_resolved)
                {
                    return Task.FromResult(_platformInstance != null
                        ? CommonResult.Ok()
                        : CommonResult.Failure(CommonErrorType.COMMON_UNKNOWN, "Google Play Games plugin not found."));
                }

                _resolved = true;

                try
                {
                    var platformType = Type.GetType("GooglePlayGames.PlayGamesPlatform, Google.Play.Games");
                    if (platformType == null)
                    {
                        return Task.FromResult(CommonResult.Failure(
                            CommonErrorType.COMMON_UNKNOWN,
                            "Google Play Games v2 plugin not found."));
                    }

                    _platformInstance = platformType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                    if (_platformInstance == null)
                    {
                        var activate = platformType.GetMethod("Activate", BindingFlags.Public | BindingFlags.Static);
                        activate?.Invoke(null, null);
                        _platformInstance = platformType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                    }

                    if (_platformInstance == null)
                    {
                        return Task.FromResult(CommonResult.Failure(
                            CommonErrorType.COMMON_UNKNOWN,
                            "Google Play Games platform instance not available."));
                    }

                    _reportScoreMethod = findMethod(
                        platformType,
                        "ReportScore",
                        typeof(long), typeof(string), typeof(Action<bool>));

                    if (_reportScoreMethod == null)
                    {
                        return Task.FromResult(CommonResult.Failure(
                            CommonErrorType.COMMON_UNKNOWN,
                            "Google Play Games API surface is missing required methods."));
                    }

                    return Task.FromResult(CommonResult.Ok());
                }
                catch (Exception ex)
                {
                    return Task.FromResult(CommonResult.Failure(CommonErrorType.COMMON_SERVER, ex.Message));
                }
#else
                return Task.FromResult(CommonResult.Failure(
                    CommonErrorType.LOGIN_UNSUPPORTED,
                    "Google adapter is not available on this platform."));
#endif
            }

            public async Task<CommonResult> ReportScoreAsync(string platformLeaderboardId, long score, CancellationToken ct)
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                var init = await InitializeAsync(ct);
                if (init.IsFailure)
                    return init;

                var auth = ensureAuthenticated();
                if (auth.IsFailure)
                    return auth;

                try
                {
                    var tcs = new TaskCompletionSource<bool>();
                    var callback = new Action<bool>(success => tcs.TrySetResult(success));

                    _reportScoreMethod.Invoke(_platformInstance, new object[]
                    {
                        score,
                        platformLeaderboardId,
                        callback,
                    });

                    var success = await tcs.Task;
                    return success
                        ? CommonResult.Ok()
                        : CommonResult.Failure(CommonErrorType.COMMON_SERVER, "GPGS score report failed.");
                }
                catch (Exception ex)
                {
                    return CommonResult.Failure(CommonErrorType.COMMON_SERVER, ex.Message);
                }
#else
                return CommonResult.Failure(CommonErrorType.LOGIN_UNSUPPORTED, "Google adapter is not available on this platform.");
#endif
            }

            private static MethodInfo findMethod(Type type, string methodName, params Type[] paramTypes)
            {
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                for (var i = 0; i < methods.Length; i++)
                {
                    var m = methods[i];
                    if (!string.Equals(m.Name, methodName, StringComparison.Ordinal))
                        continue;

                    var ps = m.GetParameters();
                    if (ps.Length != paramTypes.Length)
                        continue;

                    var matched = true;
                    for (var p = 0; p < ps.Length; p++)
                    {
                        if (ps[p].ParameterType != paramTypes[p])
                        {
                            matched = false;
                            break;
                        }
                    }

                    if (matched)
                        return m;
                }

                return null;
            }

            private static CommonResult ensureAuthenticated()
            {
                if (Social.localUser != null && Social.localUser.authenticated)
                    return CommonResult.Ok();

                return CommonResult.Failure(CommonErrorType.COMMON_AUTH, "Google Play Games authentication required.");
            }
        }
    }
}
