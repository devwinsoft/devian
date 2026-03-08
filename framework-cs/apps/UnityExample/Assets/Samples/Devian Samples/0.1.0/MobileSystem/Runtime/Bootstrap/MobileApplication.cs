using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Devian
{
    [RequireComponent(typeof(AccountManager))]
    [RequireComponent(typeof(InventoryManager))]
    [RequireComponent(typeof(PurchaseManager))]
    [RequireComponent(typeof(AchieveManager))]
    [RequireComponent(typeof(LeaderboardManager))]
    [RequireComponent(typeof(LeaderboardSeasonRewardManager))]
    [RequireComponent(typeof(GameMessageManager))]
    [RequireComponent(typeof(MissionManager))]
    [RequireComponent(typeof(SaveDataManager))]
    [RequireComponent(typeof(InputManager))]
    [RequireComponent(typeof(FirebaseManager))]
    [RequireComponent(typeof(AnalyzeManager))]
    public abstract class MobileApplication : ApplicationManager
    {
        const string FirebaseFunctionsRegion = "asia-northeast3";

        protected override Task OnBootProc()
        {
            // MobileSystem common initialization
            Log.SetSink(new UnityLogSink());

            configureFunctionsRegion();

            // Must be activated before Google login is attempted.
            #if UNITY_ANDROID && !UNITY_EDITOR
            tryActivateGooglePlayGames();
            #endif

            return Task.CompletedTask;
        }

        protected override void OnEnterForeground()
        {
            _ = refreshMissionClockAsync();
        }

        /// <summary>
        /// 플랫폼별 빌드 넘버(int)를 반환한다.
        /// Android: PackageInfo.versionCode
        /// Editor/기타: 0
        /// </summary>
        public static int GetVersionCode()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var context = activity.Call<AndroidJavaObject>("getApplicationContext");
                using var pm = context.Call<AndroidJavaObject>("getPackageManager");
                using var info = pm.Call<AndroidJavaObject>("getPackageInfo", context.Call<string>("getPackageName"), 0);
                return info.Get<int>("versionCode");
            }
            catch (Exception)
            {
                return 0;
            }
#else
            return 0;
#endif
        }

        #if UNITY_ANDROID && !UNITY_EDITOR
        private static void tryActivateGooglePlayGames()
        {
            // Best-effort activation:
            // - If GPGS plugin exists, call GooglePlayGames.PlayGamesPlatform.Activate()
            // - If not, do nothing (avoid compile error)
            try
            {
                // GPGS v2 first, then v1 fallback.
                var platformType = Type.GetType("GooglePlayGames.PlayGamesPlatform, Google.Play.Games");

                if (platformType == null)
                {
                    platformType = Type.GetType("GooglePlayGames.PlayGamesPlatform, GooglePlayGames");
                }

                if (platformType == null)
                {
                    return;
                }

                var activate = platformType.GetMethod(
                    "Activate",
                    BindingFlags.Public | BindingFlags.Static,
                    binder: null,
                    types: Type.EmptyTypes,
                    modifiers: null);

                if (activate == null)
                {
                    return;
                }

                activate.Invoke(null, null);
            }
            catch (Exception)
            {
            }
        }
        #endif

        /// <summary>
        /// MissionClockSnapshot의 버전 정보와 AppVersion을 비교하여 업데이트 필요 여부를 판정한다.
        /// MissionManager.InitializeAsync() 또는 RefreshClockAsync() 이후에 호출해야 한다.
        /// </summary>
        public VersionCheckResult VersionCheck()
        {
            if (!MissionManager.TryGet(out var missionManager))
                return VersionCheckResult.Success;

            var snapshot = missionManager.Storage.clockSnapshot;
            if (snapshot == null)
                return VersionCheckResult.Success;

            // minVersion 체크 (ForceUpdate)
            if (!string.IsNullOrEmpty(snapshot.minVersion)
                && VersionNumber.TryParse(snapshot.minVersion, out var min)
                && AppVersion < min)
            {
                return VersionCheckResult.ForceUpdate;
            }

            // currentVersion 체크 (RecommendUpdate)
            if (!string.IsNullOrEmpty(snapshot.currentVersion)
                && VersionNumber.TryParse(snapshot.currentVersion, out var recommend)
                && AppVersion < recommend)
            {
                return VersionCheckResult.RecommendUpdate;
            }

            return VersionCheckResult.Success;
        }

        private static async Task refreshMissionClockAsync()
        {
            if (!MissionManager.TryGet(out var missionManager))
                return;

            if (!missionManager.IsInitialized)
                return;

            var refresh = await missionManager.RefreshClockAsync(CancellationToken.None);
            if (refresh.IsFailure)
            {
                Debug.LogWarning($"[MobileApplication] Mission clock refresh failed on foreground: {refresh.Error}");
                return;
            }

            if (!LeaderboardSeasonRewardManager.TryGet(out var seasonRewardManager)
                || seasonRewardManager == null
                || !seasonRewardManager.IsInitialized)
            {
                return;
            }

            var syncSeasonReward = await seasonRewardManager.SyncSeasonTransitionRewardsAsync(CancellationToken.None);
            if (syncSeasonReward.IsFailure)
            {
                Debug.LogWarning($"[MobileApplication] Season reward sync failed on foreground: {syncSeasonReward.Error}");
            }
        }

        void configureFunctionsRegion()
        {
            GetComponent<FirebaseManager>()?.SetFunctionsRegion(FirebaseFunctionsRegion);
        }
    }
}
