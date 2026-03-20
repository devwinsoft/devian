using System;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
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
    [RequireComponent(typeof(RemoteDataManager))]
    [RequireComponent(typeof(SaveDataManager))]
    [RequireComponent(typeof(LoginManager))]
    [RequireComponent(typeof(InputManager))]
    [RequireComponent(typeof(FirebaseCallableManager))]
    [RequireComponent(typeof(AnalyzeManager))]
    [RequireComponent(typeof(AdsManager))]
    [RequireComponent(typeof(ShopManager))]
    public abstract class MobileApplication : BaseApplication
    {
        public new static MobileApplication Instance => BaseApplication.Instance as MobileApplication;
        public new static MobileApplication Create() => BaseApplication.Create<MobileApplication>();

        [SerializeField] public SystemLanguage DefaultLanguage = SystemLanguage.Korean;

        [SerializeField] public string FirebaseFunctionsRegion = "asia-northeast3";
        [SerializeField] public string VersionCheckAOS = string.Empty;
        [SerializeField] public string VersionCheckIOS = string.Empty;
        [SerializeField] private UI_CANVAS_ID _toastCanvasId;

        // ── Crypto (AES-256-CBC) ──

        [SerializeField] CString _cryptoKey;
        [SerializeField] CString _cryptoIv;

        public string CryptoKey => _cryptoKey;
        public string CryptoIv => _cryptoIv;

        public static string EncryptJson(string plainJson, string keyBase64, string ivBase64)
        {
            if (string.IsNullOrEmpty(plainJson))
                return string.Empty;

            using var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = Convert.FromBase64String(keyBase64);
            aes.IV = Convert.FromBase64String(ivBase64);

            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plainJson);
            var encrypted = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            return Convert.ToBase64String(encrypted);
        }

        public static string DecryptJson(string encryptedBase64, string keyBase64, string ivBase64)
        {
            if (string.IsNullOrEmpty(encryptedBase64))
                return string.Empty;

            using var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = Convert.FromBase64String(keyBase64);
            aes.IV = Convert.FromBase64String(ivBase64);

            using var decryptor = aes.CreateDecryptor();
            var encryptedBytes = Convert.FromBase64String(encryptedBase64);
            var decrypted = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
            return Encoding.UTF8.GetString(decrypted);
        }

        protected override Task onBootAsync()
        {
            // MobilePackage common initialization
            Log.SetSink(new UnityLogSink());

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

            return onLoadCompletedInternalAsync();
        }

        private async Task onLoadCompletedInternalAsync()
        {
            await PreloadUITransitionPresetsAsync();
            BootstrapToastCanvas();
        }

        private static async Task PreloadUITransitionPresetsAsync()
        {
            var settings = Resources.Load<BundleSettings>(BundleSettings.ResourcesPath);
            var presetCacheKey = settings != null
                ? settings.GetEntry("UI_TRANSITION_PRESET_ID")
                : string.Empty;
            if (string.IsNullOrWhiteSpace(presetCacheKey))
            {
                presetCacheKey = "UI_TRANSITION_PRESET_ID";
            }

            const string presetLoadLabel = "ui";
            var loadResult = await AssetManager.LoadBundleAssets<UITransitionPresetAsset>(presetCacheKey, presetLoadLabel);
            if (loadResult.IsFailure)
            {
                Debug.LogWarning($"[MobileApplication] Failed to preload UI transition presets from '{presetLoadLabel}': {loadResult.Error?.Message}");
            }
        }

        private void BootstrapToastCanvas()
        {
            var toastCanvas = UIToastCanvas.Instance;
            if (toastCanvas == null)
            {
                toastCanvas = FindAnyObjectByType<UIToastCanvas>(FindObjectsInactive.Include);
            }

            if (toastCanvas == null)
            {
                if (_toastCanvasId == null || !_toastCanvasId.IsValid)
                {
                    return;
                }

                try
                {
                    toastCanvas = BundlePool.Spawn<UIToastCanvas>(_toastCanvasId);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[MobileApplication] Failed to spawn UIToastCanvas '{_toastCanvasId.Value}': {e.Message}");
                    return;
                }
            }

            if (toastCanvas == null)
            {
                return;
            }

            DontDestroyOnLoad(toastCanvas.gameObject);

            if (!toastCanvas.isInitialized)
            {
                toastCanvas.Init();
            }
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
