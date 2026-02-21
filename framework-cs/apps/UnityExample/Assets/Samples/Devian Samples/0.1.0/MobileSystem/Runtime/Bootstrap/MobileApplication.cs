using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Devian
{
    [RequireComponent(typeof(AccountManager))]
    [RequireComponent(typeof(SaveDataManager))]
    [RequireComponent(typeof(GameStorageManager))]
    public abstract class MobileApplication : BaseBootstrap
    {
        protected override IEnumerator OnBootProc()
        {
            // MobileSystem common initialization
            Log.SetSink(new UnityLogSink());

            // Must be activated before Google login is attempted.
            #if UNITY_ANDROID && !UNITY_EDITOR
            tryActivateGooglePlayGames();
            #endif

            yield break;
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
    }
}
