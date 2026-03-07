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
    internal interface IAchievePlatformAdapter
    {
        Task<CommonResult> InitializeAsync(CancellationToken ct);
        Task<CommonResult> UnlockAchievementAsync(string platformAchievementId, CancellationToken ct);
        Task<CommonResult<Dictionary<string, bool>>> FetchAchievementStatesAsync(CancellationToken ct);
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
        public Task<CommonResult> InitializeAsync(CancellationToken ct)
            => Task.FromResult(CommonResult.Failure(
                CommonErrorType.LOGIN_UNSUPPORTED,
                "Achievement is not supported on this platform."));

        public Task<CommonResult> UnlockAchievementAsync(string platformAchievementId, CancellationToken ct)
            => Task.FromResult(CommonResult.Failure(
                CommonErrorType.LOGIN_UNSUPPORTED,
                "Achievement is not supported on this platform."));

        public Task<CommonResult<Dictionary<string, bool>>> FetchAchievementStatesAsync(CancellationToken ct)
            => Task.FromResult(CommonResult<Dictionary<string, bool>>.Failure(
                CommonErrorType.LOGIN_UNSUPPORTED,
                "Achievement is not supported on this platform."));
    }

    internal sealed class AppleAchievePlatformAdapter : IAchievePlatformAdapter
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

        public async Task<CommonResult> UnlockAchievementAsync(string platformAchievementId, CancellationToken ct)
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
                        ? CommonResult.Ok()
                        : CommonResult.Failure(CommonErrorType.COMMON_SERVER, "Game Center achievement unlock failed.");
                }
                catch (Exception ex)
                {
                    return CommonResult.Failure(CommonErrorType.COMMON_SERVER, ex.Message);
                }
#else
            return CommonResult.Failure(CommonErrorType.LOGIN_UNSUPPORTED, "Game Center adapter is not available on this platform.");
#endif
        }

        public async Task<CommonResult<Dictionary<string, bool>>> FetchAchievementStatesAsync(CancellationToken ct)
        {
#if UNITY_IOS && !UNITY_EDITOR
                var auth = ensureAuthenticated();
                if (auth.IsFailure)
                    return CommonResult<Dictionary<string, bool>>.Failure(auth.Error!);

                try
                {
                    var tcs = new TaskCompletionSource<IAchievement[]>();
                    Social.LoadAchievements(achievements => tcs.TrySetResult(achievements));
                    var achievements = await tcs.Task;

                    if (achievements == null)
                    {
                        return CommonResult<Dictionary<string, bool>>.Failure(
                            CommonErrorType.COMMON_SERVER,
                            "Game Center achievement sync failed.");
                    }

                    return CommonResult<Dictionary<string, bool>>.Success(
                        AchievePlatformHelper.ToAchievementStateMap(achievements));
                }
                catch (Exception ex)
                {
                    return CommonResult<Dictionary<string, bool>>.Failure(CommonErrorType.COMMON_SERVER, ex.Message);
                }
#else
            return CommonResult<Dictionary<string, bool>>.Failure(
                CommonErrorType.LOGIN_UNSUPPORTED,
                "Game Center adapter is not available on this platform.");
#endif
        }

        static CommonResult ensureAuthenticated()
        {
            if (Social.localUser != null && Social.localUser.authenticated)
                return CommonResult.Ok();

            return CommonResult.Failure(CommonErrorType.COMMON_AUTH, "Game Center authentication required.");
        }
    }

    internal sealed class GoogleAchievePlatformAdapter : IAchievePlatformAdapter
    {
        private bool _resolved;
        private object _platformInstance;
        private MethodInfo _reportProgressMethod;
        private MethodInfo _loadAchievementsMethod;

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

        public async Task<CommonResult> UnlockAchievementAsync(string platformAchievementId, CancellationToken ct)
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
                        ? CommonResult.Ok()
                        : CommonResult.Failure(CommonErrorType.COMMON_SERVER, "GPGS achievement unlock failed.");
                }
                catch (Exception ex)
                {
                    return CommonResult.Failure(CommonErrorType.COMMON_SERVER, ex.Message);
                }
#else
            return CommonResult.Failure(CommonErrorType.LOGIN_UNSUPPORTED, "Google adapter is not available on this platform.");
#endif
        }

        public async Task<CommonResult<Dictionary<string, bool>>> FetchAchievementStatesAsync(CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
                var init = await InitializeAsync(ct);
                if (init.IsFailure)
                    return CommonResult<Dictionary<string, bool>>.Failure(init.Error!);

                var auth = ensureAuthenticated();
                if (auth.IsFailure)
                    return CommonResult<Dictionary<string, bool>>.Failure(auth.Error!);

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
                        return CommonResult<Dictionary<string, bool>>.Failure(
                            CommonErrorType.COMMON_SERVER,
                            "GPGS achievement sync failed.");
                    }

                    return CommonResult<Dictionary<string, bool>>.Success(
                        AchievePlatformHelper.ToAchievementStateMap(achievements));
                }
                catch (Exception ex)
                {
                    return CommonResult<Dictionary<string, bool>>.Failure(CommonErrorType.COMMON_SERVER, ex.Message);
                }
#else
            return CommonResult<Dictionary<string, bool>>.Failure(
                CommonErrorType.LOGIN_UNSUPPORTED,
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

        static CommonResult ensureAuthenticated()
        {
            if (Social.localUser != null && Social.localUser.authenticated)
                return CommonResult.Ok();

            return CommonResult.Failure(CommonErrorType.COMMON_AUTH, "Google Play Games authentication required.");
        }
    }
}
