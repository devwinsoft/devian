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
    [RequireComponent(typeof(GameMessageManager))]
    [RequireComponent(typeof(AttendManager))]
    [RequireComponent(typeof(MissionManager))]
    [RequireComponent(typeof(RemoteConfigManager))]
    [RequireComponent(typeof(SaveDataManager))]
    [RequireComponent(typeof(LoginManager))]
    [RequireComponent(typeof(InputManager))]
    [RequireComponent(typeof(FirebaseCallableManager))]
    [RequireComponent(typeof(AnalyzeManager))]
    public abstract class MobileApplication : BaseApplication
    {
        public new static MobileApplication Create() => BaseApplication.Create<MobileApplication>();

        const string FirebaseFunctionsRegion = "asia-northeast3";

        protected override Task onBootAsync()
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

        protected override Task onLoadCompletedAsync()
        {
            // 서버와 별개로 독립적으로 동작하는 Manager 초기화
            var initMessage = GameMessageManager.Instance.Initialize();
            if (initMessage.IsFailure)
            {
                Debug.LogError($"[MobileApplication] GameMessageManager.Initialize failed: {initMessage.Error.Code}: {initMessage.Error.Message}");
            }

            return Task.CompletedTask;
        }

        protected override void OnEnterForeground()
        {
            _ = refreshRemoteConfigAsync();
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

        private static async Task refreshRemoteConfigAsync()
        {
            if (!RemoteConfigManager.TryGet(out var remoteConfigManager))
                return;

            if (!remoteConfigManager.IsInitialized)
                return;

            var refresh = await remoteConfigManager.RefreshAsync(CancellationToken.None);
            if (refresh.IsFailure)
            {
                Debug.LogWarning($"[MobileApplication] Remote config refresh failed on foreground: {refresh.Error}");
                return;
            }

            if (!LeaderboardManager.TryGet(out var leaderboardManager)
                || leaderboardManager == null
                || !leaderboardManager.IsInitialized)
            {
                return;
            }

            var syncSeasonReward = await leaderboardManager.SyncSeasonTransitionRewardsAsync(CancellationToken.None);
            if (syncSeasonReward.IsFailure)
            {
                Debug.LogWarning($"[MobileApplication] Season reward sync failed on foreground: {syncSeasonReward.Error}");
            }
        }

        void configureFunctionsRegion()
        {
            GetComponent<FirebaseCallableManager>()?.SetFunctionsRegion(FirebaseFunctionsRegion);
        }
    }
}
