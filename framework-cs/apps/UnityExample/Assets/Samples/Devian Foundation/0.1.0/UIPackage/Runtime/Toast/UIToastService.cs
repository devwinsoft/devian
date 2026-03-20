using UnityEngine;

namespace Devian
{
    public sealed class UIToastService : AutoSingleton<UIToastService>
    {
        private static readonly ToastGroupConfig[] s_emptyGroupConfigs = System.Array.Empty<ToastGroupConfig>();

        private UIToastSettings _settings;
        private bool _settingsLoaded;
        private bool _missingSettingsWarningLogged;

        public UIToastSettings Settings => ResolveSettings();

        public void Show(
            string message,
            string groupId = UIToastDefaults.DefaultGroupId,
            float? durationOverride = null,
            ToastType toastType = ToastType.Info)
        {
            Show(new ToastRequest(groupId, message, durationOverride, toastType));
        }

        public void Show(ToastRequest request)
        {
            var panel = ResolvePanel();
            if (panel == null)
            {
                Debug.LogWarning("[UIToastService] UIToastPanel is unavailable. Toast request skipped.");
                return;
            }

            panel.Enqueue(request);
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

        private UIToastSettings ResolveSettings()
        {
            if (_settingsLoaded)
            {
                return _settings;
            }

            _settings = Resources.Load<UIToastSettings>(UIToastSettings.ResourcesPath);
            _settingsLoaded = true;

            if (_settings == null && !_missingSettingsWarningLogged)
            {
                _missingSettingsWarningLogged = true;
                Debug.LogWarning($"[UIToastService] UIToastSettings not found at Resources path '{UIToastSettings.ResourcesPath}'. Default group config will be used.");
            }

            return _settings;
        }
    }
}
