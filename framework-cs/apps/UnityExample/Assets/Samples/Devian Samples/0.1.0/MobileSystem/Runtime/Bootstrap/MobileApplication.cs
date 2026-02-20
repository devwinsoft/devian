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

            // Purchase store injection (platform-dependent packages)
            trySetPurchaseStore();

            yield break;
        }

        private static void trySetPurchaseStore()
        {
#if UNITY_IOS || UNITY_TVOS
            // Devian.PurchaseStoreApple, Devian.Purchase.Store.Apple
            var t = Type.GetType("Devian.PurchaseStoreApple, Devian.Purchase.Store.Apple");
#elif UNITY_ANDROID
            // Devian.PurchaseStoreGoogle, Devian.Purchase.Store.Google
            var t = Type.GetType("Devian.PurchaseStoreGoogle, Devian.Purchase.Store.Google");
#else
            Type t = null;
#endif
            if (t == null)
                return;

            var instance = Activator.CreateInstance(t) as IPurchaseStore;
            if (instance == null)
                return;

            Singleton.Get<PurchaseManager>().SetPurchaseStore(instance);
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
                    platformType = Type.GetType("GooglePlayGames.PlayGamesPlatform, GooglePlayGames");

                if (platformType == null)
                    return;

                var activate = platformType.GetMethod(
                    "Activate",
                    BindingFlags.Public | BindingFlags.Static,
                    binder: null,
                    types: Type.EmptyTypes,
                    modifiers: null);

                activate?.Invoke(null, null);
            }
            catch
            {
                // Intentionally ignore: plugin may be partially missing or stripped.
            }
        }
        #endif
    }
}
