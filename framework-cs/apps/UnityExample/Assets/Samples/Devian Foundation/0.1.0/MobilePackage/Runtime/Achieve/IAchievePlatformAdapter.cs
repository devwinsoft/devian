using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Devian.Domain.Game;
using UnityEngine;
using UnityEngine.SocialPlatforms;

namespace Devian
{
    internal interface IAchievePlatformAdapter
    {
        Task<GameResult> InitializeAsync(CancellationToken ct);
        Task<GameResult> UnlockAchievementAsync(string platformAchievementId, CancellationToken ct);
        Task<GameResult<Dictionary<string, bool>>> FetchAchievementStatesAsync(CancellationToken ct);
    }

    internal static class AchievePlatformHelper
    {
        internal static Dictionary<string, bool> ToAchievementStateMap(IAchievement[] achievements)
        {
            var map = new Dictionary<string, bool>(StringComparer.Ordinal);

            for (var i = 0; i < achievements.Length; i++)
            {
                var achievement = achievements[i];
                if (achievement == null || string.IsNullOrEmpty(achievement.id))
                    continue;

                var unlocked = achievement.completed || achievement.percentCompleted >= 100d;
                if (map.TryGetValue(achievement.id, out var prev))
                    map[achievement.id] = prev || unlocked;
                else
                    map[achievement.id] = unlocked;
            }

            return map;
        }
    }

    internal sealed class UnsupportedAchievePlatformAdapter : IAchievePlatformAdapter
    {
        public Task<GameResult> InitializeAsync(CancellationToken ct)
            => Task.FromResult(GameResult.Failure(
                GAME_ERROR_TYPE.LOGIN_UNSUPPORTED,
                "Achievement is not supported on this platform."));

        public Task<GameResult> UnlockAchievementAsync(string platformAchievementId, CancellationToken ct)
            => Task.FromResult(GameResult.Failure(
                GAME_ERROR_TYPE.LOGIN_UNSUPPORTED,
                "Achievement is not supported on this platform."));

        public Task<GameResult<Dictionary<string, bool>>> FetchAchievementStatesAsync(CancellationToken ct)
            => Task.FromResult(GameResult<Dictionary<string, bool>>.Failure(
                GAME_ERROR_TYPE.LOGIN_UNSUPPORTED,
                "Achievement is not supported on this platform."));
    }

    internal sealed class AppleAchievePlatformAdapter : IAchievePlatformAdapter
    {
        public Task<GameResult> InitializeAsync(CancellationToken ct)
        {
#if UNITY_IOS && !UNITY_EDITOR
                return Task.FromResult(GameResult.Ok());
#else
            return Task.FromResult(GameResult.Failure(
                GAME_ERROR_TYPE.LOGIN_UNSUPPORTED,
                "Game Center adapter is not available on this platform."));
#endif
        }

        public async Task<GameResult> UnlockAchievementAsync(string platformAchievementId, CancellationToken ct)
        {
#if UNITY_IOS && !UNITY_EDITOR
                var auth = ensureAuthenticated();
                if (auth.IsFailure)
                    return auth;

                try
                {
                    var tcs = new TaskCompletionSource<bool>();
                    Social.ReportProgress(platformAchievementId, 100d, success => tcs.TrySetResult(success));
                    var success = await tcs.Task;

                    return success
                        ? GameResult.Ok()
                        : GameResult.Failure(GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT, "Game Center achievement unlock failed.");
                }
                catch (Exception ex)
                {
                    return GameResult.Failure(GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT, ex.Message);
                }
#else
            return GameResult.Failure(GAME_ERROR_TYPE.LOGIN_UNSUPPORTED, "Game Center adapter is not available on this platform.");
#endif
        }

        public async Task<GameResult<Dictionary<string, bool>>> FetchAchievementStatesAsync(CancellationToken ct)
        {
#if UNITY_IOS && !UNITY_EDITOR
                var auth = ensureAuthenticated();
                if (auth.IsFailure)
                    return GameResult<Dictionary<string, bool>>.Failure(auth.Error!);

                try
                {
                    var tcs = new TaskCompletionSource<IAchievement[]>();
                    Social.LoadAchievements(achievements => tcs.TrySetResult(achievements));
                    var achievements = await tcs.Task;

                    if (achievements == null)
                    {
                        return GameResult<Dictionary<string, bool>>.Failure(
                            GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                            "Game Center achievement sync failed.");
                    }

                    return GameResult<Dictionary<string, bool>>.Success(
                        AchievePlatformHelper.ToAchievementStateMap(achievements));
                }
                catch (Exception ex)
                {
                    return GameResult<Dictionary<string, bool>>.Failure(GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT, ex.Message);
                }
#else
            return GameResult<Dictionary<string, bool>>.Failure(
                GAME_ERROR_TYPE.LOGIN_UNSUPPORTED,
                "Game Center adapter is not available on this platform.");
#endif
        }

        static GameResult ensureAuthenticated()
        {
            if (Social.localUser != null && Social.localUser.authenticated)
                return GameResult.Ok();

            return GameResult.Failure(GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT, "Game Center authentication required.");
        }
    }

    internal sealed class GoogleAchievePlatformAdapter : IAchievePlatformAdapter
    {
        private bool _resolved;
        private object _platformInstance;
        private MethodInfo _reportProgressMethod;
        private MethodInfo _loadAchievementsMethod;

        public Task<GameResult> InitializeAsync(CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (_resolved)
                {
                    return Task.FromResult(_platformInstance != null
                        ? GameResult.Ok()
                        : GameResult.Failure(GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT, "Google Play Games plugin not found."));
                }

                _resolved = true;

                try
                {
                    var platformType = Type.GetType("GooglePlayGames.PlayGamesPlatform, Google.Play.Games");
                    if (platformType == null)
                    {
                        return Task.FromResult(GameResult.Failure(
                            GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
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
                        return Task.FromResult(GameResult.Failure(
                            GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                            "Google Play Games platform instance not available."));
                    }

                    _reportProgressMethod = findMethod(
                        platformType,
                        "ReportProgress",
                        typeof(string), typeof(double), typeof(Action<bool>));

                    _loadAchievementsMethod = findMethod(
                        platformType,
                        "LoadAchievements",
                        typeof(Action<IAchievement[]>));

                    if (_reportProgressMethod == null || _loadAchievementsMethod == null)
                    {
                        return Task.FromResult(GameResult.Failure(
                            GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                            "Google Play Games API surface is missing required methods."));
                    }

                    return Task.FromResult(GameResult.Ok());
                }
                catch (Exception ex)
                {
                    return Task.FromResult(GameResult.Failure(GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT, ex.Message));
                }
#else
            return Task.FromResult(GameResult.Failure(
                GAME_ERROR_TYPE.LOGIN_UNSUPPORTED,
                "Google adapter is not available on this platform."));
#endif
        }

        public async Task<GameResult> UnlockAchievementAsync(string platformAchievementId, CancellationToken ct)
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

                    _reportProgressMethod.Invoke(_platformInstance, new object[]
                    {
                        platformAchievementId,
                        100d,
                        callback,
                    });

                    var success = await tcs.Task;
                    return success
                        ? GameResult.Ok()
                        : GameResult.Failure(GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT, "GPGS achievement unlock failed.");
                }
                catch (Exception ex)
                {
                    return GameResult.Failure(GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT, ex.Message);
                }
#else
            return GameResult.Failure(GAME_ERROR_TYPE.LOGIN_UNSUPPORTED, "Google adapter is not available on this platform.");
#endif
        }

        public async Task<GameResult<Dictionary<string, bool>>> FetchAchievementStatesAsync(CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
                var init = await InitializeAsync(ct);
                if (init.IsFailure)
                    return GameResult<Dictionary<string, bool>>.Failure(init.Error!);

                var auth = ensureAuthenticated();
                if (auth.IsFailure)
                    return GameResult<Dictionary<string, bool>>.Failure(auth.Error!);

                try
                {
                    var tcs = new TaskCompletionSource<IAchievement[]>();
                    var callback = new Action<IAchievement[]>(achievements => tcs.TrySetResult(achievements));

                    _loadAchievementsMethod.Invoke(_platformInstance, new object[]
                    {
                        callback,
                    });

                    var achievements = await tcs.Task;
                    if (achievements == null)
                    {
                        return GameResult<Dictionary<string, bool>>.Failure(
                            GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                            "GPGS achievement sync failed.");
                    }

                    return GameResult<Dictionary<string, bool>>.Success(
                        AchievePlatformHelper.ToAchievementStateMap(achievements));
                }
                catch (Exception ex)
                {
                    return GameResult<Dictionary<string, bool>>.Failure(GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT, ex.Message);
                }
#else
            return GameResult<Dictionary<string, bool>>.Failure(
                GAME_ERROR_TYPE.LOGIN_UNSUPPORTED,
                "Google adapter is not available on this platform.");
#endif
        }

        static MethodInfo findMethod(Type type, string methodName, params Type[] paramTypes)
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

        static GameResult ensureAuthenticated()
        {
            if (Social.localUser != null && Social.localUser.authenticated)
                return GameResult.Ok();

            return GameResult.Failure(GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT, "Google Play Games authentication required.");
        }
    }
}
