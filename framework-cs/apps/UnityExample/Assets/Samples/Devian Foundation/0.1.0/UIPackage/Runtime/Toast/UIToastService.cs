using System;
using UnityEngine;

namespace Devian
{
    public sealed class UIToastService : AutoSingleton<UIToastService>
    {
        private static readonly ToastGroupConfig[] s_emptyGroupConfigs = Array.Empty<ToastGroupConfig>();

        private UISettings _settings;
        private bool _settingsLoaded;
        private bool _missingSettingsWarningLogged;

        public UISettings Settings => ResolveSettings();

        /// <summary>
        /// Toast system을 초기화한다.
        /// UISettings에서 canvas ID를 읽어 UIToastCanvas를 spawn/init한다.
        /// MobileApplication.onLoadCompletedAsync()에서 1회 호출한다.
        /// </summary>
        public void Initialize()
        {
            var canvas = UIToastCanvas.Instance;
            if (canvas == null)
            {
                canvas = FindAnyObjectByType<UIToastCanvas>(FindObjectsInactive.Include);
            }

            if (canvas == null)
            {
                var settings = ResolveSettings();
                var canvasId = settings != null ? settings.ToastCanvasId : null;
                if (canvasId == null || !canvasId.IsValid)
                {
                    return;
                }

                try
                {
                    canvas = BundlePool.Spawn<UIToastCanvas>(canvasId);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[UIToastService] Failed to spawn UIToastCanvas '{canvasId.Value}': {e.Message}");
                    return;
                }
            }

            if (canvas == null)
            {
                return;
            }

            DontDestroyOnLoad(canvas.gameObject);

            if (!canvas.isInitialized)
            {
                canvas.Init();
            }
        }

        public void Show(
            string message,
            string groupId = UIToastDefaults.DefaultGroupId,
            ToastType toastType = ToastType.Info)
        {
            var panel = ResolvePanel();
            if (panel == null)
            {
                Debug.LogWarning("[UIToastService] UIToastPanel is unavailable. Toast request skipped.");
                return;
            }

            panel.Enqueue(message, groupId, toastType);
        }

        public ToastGroupConfig[] GetGroupConfigs()
        {
            var settings = Settings;
            if (settings == null || settings.GroupConfigs == null)
            {
                return s_emptyGroupConfigs;
            }

            return settings.GroupConfigs;
        }

        private UIToastPanel ResolvePanel()
        {
            var canvas = UIToastCanvas.Instance;
            if (canvas == null)
            {
                canvas = FindAnyObjectByType<UIToastCanvas>(FindObjectsInactive.Include);
            }

            if (canvas == null)
            {
                return null;
            }

            if (!canvas.isInitialized || !canvas.isInitComplete)
            {
                Debug.LogWarning("[UIToastService] UIToastCanvas exists but is not initialized yet.", canvas);
                return null;
            }

            if (canvas.panel == null)
            {
                Debug.LogWarning("[UIToastService] UIToastCanvas.panel is not assigned.", canvas);
                return null;
            }

            return canvas.panel;
        }

        private UISettings ResolveSettings()
        {
            if (_settingsLoaded)
            {
                return _settings;
            }

            _settings = Resources.Load<UISettings>(UISettings.ResourcesPath);
            _settingsLoaded = true;

            if (_settings == null && !_missingSettingsWarningLogged)
            {
                _missingSettingsWarningLogged = true;
                Debug.LogWarning($"[UIToastService] UISettings not found at Resources path '{UISettings.ResourcesPath}'. Default group config will be used.");
            }

            return _settings;
        }
    }
}
