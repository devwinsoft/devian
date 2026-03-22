using System;
using System.Collections.Generic;
using UnityEngine;

namespace Devian
{
    public sealed class UIPopupManager : AutoSingleton<UIPopupManager>
    {
        private sealed class PopupStackEntry
        {
            public PopupConfig Config;
            public UIPopupFrame Frame;
            public Action<PopupResult> OnClosed;
            public PopupFrameState State;
            public object PendingResultPayload;
            public PopupCloseReason PendingCloseReason;
        }

        private readonly List<PopupStackEntry> _stack = new List<PopupStackEntry>();
        private readonly Dictionary<UIPopupFrame, PopupStackEntry> _entryByFrame = new Dictionary<UIPopupFrame, PopupStackEntry>();

        private UISettings _settings;
        private bool _settingsLoaded;
        private bool _missingSettingsWarningLogged;
        private bool _missingCanvasWarningLogged;

        public UISettings Settings => ResolveSettings();

        /// <summary>
        /// Popup system을 초기화한다.
        /// UISettings에서 canvas ID를 읽어 UIPopupCanvas를 spawn/init한다.
        /// MobileApplication.onLoadCompletedAsync()에서 1회 호출한다.
        /// </summary>
        public void Initialize()
        {
            var canvas = UIPopupCanvas.Instance;
            if (canvas == null)
            {
                canvas = FindAnyObjectByType<UIPopupCanvas>(FindObjectsInactive.Include);
            }

            if (canvas == null)
            {
                var settings = ResolveSettings();
                var canvasId = settings != null ? settings.PopupCanvasId : null;
                if (canvasId == null || !canvasId.IsValid)
                {
                    return;
                }

                try
                {
                    canvas = BundlePool.Spawn<UIPopupCanvas>(canvasId);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[UIPopupManager] Failed to spawn UIPopupCanvas '{canvasId.Value}': {e.Message}");
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

        public bool Open(string popupId, object payload = null, Action<PopupResult> onClosed = null)
        {
            return Open(new PopupRequest(popupId, payload, onClosed));
        }

        public bool Open(PopupRequest request)
        {
            var panel = ResolvePanel();
            if (panel == null)
            {
                Debug.LogWarning("[UIPopupManager] UIPopupPanel is unavailable. Popup request skipped.");
                return false;
            }

            var config = ResolveConfig(request.PopupId);
            if (config == null)
            {
                Debug.LogWarning($"[UIPopupManager] PopupConfig not found for popup id '{request.PopupId}'.");
                return false;
            }

            if (config.PopupFrameId == null || !config.PopupFrameId.IsValid)
            {
                Debug.LogWarning($"[UIPopupManager] PopupFrameId is invalid for popup id '{request.PopupId}'.");
                return false;
            }

            var duplicateEntry = FindOpenedEntry(request.PopupId);
            if (!HandleDuplicate(config, request, duplicateEntry))
            {
                return false;
            }

            UIPopupFrame frame;
            try
            {
                frame = BundlePool.Spawn<UIPopupFrame>(config.PopupFrameId, parent: panel.popupRoot);
            }
            catch (Exception e)
            {
                Debug.LogError($"[UIPopupManager] Failed to spawn popup frame '{config.PopupFrameId}': {e.Message}");
                return false;
            }

            if (frame == null)
            {
                Debug.LogError($"[UIPopupManager] Spawn returned null for popup frame '{config.PopupFrameId}'.");
                return false;
            }

            panel.AttachFrame(frame);

            var entry = new PopupStackEntry
            {
                Config = config,
                Frame = frame,
                OnClosed = request.OnClosed,
                State = PopupFrameState.Opening
            };

            _stack.Add(entry);
            _entryByFrame[frame] = entry;
            RefreshStackState();

            frame.Open(config, request, HandleFrameOpened, HandleFrameCloseStarted, HandleFrameClosed);
            return true;
        }

        public bool CloseTop(PopupCloseReason reason = PopupCloseReason.Canceled)
        {
            var topEntry = GetTopEntry();
            if (topEntry == null)
            {
                return false;
            }

            RequestClose(topEntry, reason, null);
            return true;
        }

        public void CloseAll()
        {
            if (_stack.Count == 0)
            {
                return;
            }

            for (var i = _stack.Count - 1; i >= 0; i--)
            {
                var entry = _stack[i];
                ForceCloseEntry(entry, PopupCloseReason.ForceClosed, null);
            }

            _stack.Clear();
            _entryByFrame.Clear();
            RefreshStackState();
        }

        internal void HandleDimClicked()
        {
            var topEntry = GetTopEntry();
            if (topEntry == null || topEntry.Config == null || !topEntry.Config.CloseOnDimClick)
            {
                return;
            }

            RequestClose(topEntry, PopupCloseReason.DimClick, null);
        }

        private void Update()
        {
            var topEntry = GetTopEntry();
            if (topEntry == null || topEntry.Config == null)
            {
                return;
            }

            if (!Input.GetKeyDown(KeyCode.Escape))
            {
                return;
            }

            #if UNITY_ANDROID && !UNITY_EDITOR
            if (!topEntry.Config.CloseOnBack)
            {
                return;
            }

            RequestClose(topEntry, PopupCloseReason.Back, null);
            #else
            if (!topEntry.Config.CloseOnEscape)
            {
                return;
            }

            RequestClose(topEntry, PopupCloseReason.Escape, null);
            #endif
        }

        private bool HandleDuplicate(PopupConfig config, PopupRequest request, PopupStackEntry duplicateEntry)
        {
            if (duplicateEntry == null || config == null)
            {
                return true;
            }

            switch (config.DuplicatePolicy)
            {
                case PopupDuplicatePolicy.IgnoreIfOpened:
                    return false;

                case PopupDuplicatePolicy.FocusIfOpened:
                    FocusEntry(duplicateEntry);
                    return false;

                case PopupDuplicatePolicy.ReplaceIfOpened:
                    RequestClose(duplicateEntry, PopupCloseReason.Replaced, null);
                    return true;

                default:
                    return true;
            }
        }

        private void FocusEntry(PopupStackEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            _stack.Remove(entry);
            _stack.Add(entry);

            if (entry.Frame != null)
            {
                entry.Frame.transform.SetAsLastSibling();
            }

            RefreshStackState();
        }

        private void RequestClose(PopupStackEntry entry, PopupCloseReason reason, object payload)
        {
            if (entry == null || entry.Frame == null || entry.State == PopupFrameState.Closing)
            {
                return;
            }

            entry.PendingCloseReason = reason;
            entry.PendingResultPayload = payload;
            entry.State = PopupFrameState.Closing;
            RefreshStackState();
            entry.Frame.CloseFromManager(reason, payload);
        }

        private void ForceCloseEntry(PopupStackEntry entry, PopupCloseReason reason, object payload)
        {
            if (entry == null)
            {
                return;
            }

            var panel = ResolvePanel();
            if (panel != null && entry.Frame != null)
            {
                panel.DetachFrame(entry.Frame);
            }

            if (entry.Frame != null)
            {
                BundlePool.Despawn(entry.Frame);
            }

            entry.OnClosed?.Invoke(new PopupResult(entry.Config?.PopupId, reason, payload));
        }

        private void HandleFrameOpened(UIPopupFrame frame)
        {
            if (frame == null || !_entryByFrame.TryGetValue(frame, out var entry))
            {
                return;
            }

            entry.State = PopupFrameState.Opened;
            RefreshStackState();
        }

        private void HandleFrameCloseStarted(UIPopupFrame frame, PopupCloseReason reason, object payload)
        {
            if (frame == null || !_entryByFrame.TryGetValue(frame, out var entry))
            {
                return;
            }

            entry.PendingCloseReason = reason;
            entry.PendingResultPayload = payload;
            entry.State = PopupFrameState.Closing;
            RefreshStackState();
        }

        private void HandleFrameClosed(UIPopupFrame frame, PopupCloseReason reason, object payload)
        {
            if (frame == null || !_entryByFrame.TryGetValue(frame, out var entry))
            {
                return;
            }

            _entryByFrame.Remove(frame);
            _stack.Remove(entry);

            var panel = ResolvePanel();
            if (panel != null)
            {
                panel.DetachFrame(frame);
            }

            BundlePool.Despawn(frame);
            entry.OnClosed?.Invoke(new PopupResult(entry.Config?.PopupId, reason, payload));
            RefreshStackState();
        }

        private void RefreshStackState()
        {
            var topIndex = _stack.Count - 1;

            for (var i = 0; i < _stack.Count; i++)
            {
                var entry = _stack[i];
                if (entry?.Frame == null)
                {
                    continue;
                }

                var allowInput = i == topIndex && entry.State == PopupFrameState.Opened;
                entry.Frame.SetTopState(i == topIndex, allowInput);
                if (entry.Frame.transform.parent != null && i == topIndex)
                {
                    entry.Frame.transform.SetAsLastSibling();
                }
            }

            var panel = ResolvePanel();
            if (panel == null)
            {
                return;
            }

            var topEntry = GetTopEntry();
            if (topEntry == null || topEntry.Config == null)
            {
                panel.ApplyModalState(false, false, false, UIPopupDefaults.DefaultDimColor, 0f);
                return;
            }

            var settings = Settings;
            var dimColor = settings != null ? settings.DimColor : UIPopupDefaults.DefaultDimColor;
            var dimAlpha = settings != null ? settings.DimAlpha : UIPopupDefaults.DefaultDimAlpha;
            var blockBehind = topEntry.Config.BlockInputBehind || topEntry.Config.CloseOnDimClick;

            panel.ApplyModalState(
                topEntry.Config.UseDim,
                blockBehind,
                topEntry.Config.CloseOnDimClick,
                dimColor,
                dimAlpha);
        }

        private PopupStackEntry FindOpenedEntry(string popupId)
        {
            for (var i = _stack.Count - 1; i >= 0; i--)
            {
                var entry = _stack[i];
                if (entry == null || entry.Config == null)
                {
                    continue;
                }

                if (entry.State == PopupFrameState.Closing)
                {
                    continue;
                }

                if (string.Equals(entry.Config.PopupId, popupId, StringComparison.Ordinal))
                {
                    return entry;
                }
            }

            return null;
        }

        private PopupStackEntry GetTopEntry()
        {
            return _stack.Count == 0 ? null : _stack[_stack.Count - 1];
        }

        private UIPopupPanel ResolvePanel()
        {
            var canvas = UIPopupCanvas.Instance;
            if (canvas == null)
            {
                canvas = FindAnyObjectByType<UIPopupCanvas>(FindObjectsInactive.Include);
            }

            if (canvas == null)
            {
                if (!_missingCanvasWarningLogged)
                {
                    _missingCanvasWarningLogged = true;
                }
                return null;
            }

            if (!canvas.isInitialized || !canvas.isInitComplete)
            {
                Debug.LogWarning("[UIPopupManager] UIPopupCanvas exists but is not initialized yet.", canvas);
                return null;
            }

            if (canvas.panel == null)
            {
                Debug.LogWarning("[UIPopupManager] UIPopupCanvas.panel is not assigned.", canvas);
                return null;
            }

            _missingCanvasWarningLogged = false;
            return canvas.panel;
        }

        private PopupConfig ResolveConfig(string popupId)
        {
            var settings = Settings;
            if (settings == null || settings.PopupConfigs == null)
            {
                return null;
            }

            for (var i = 0; i < settings.PopupConfigs.Length; i++)
            {
                var config = settings.PopupConfigs[i];
                if (config == null)
                {
                    continue;
                }

                if (string.Equals(config.PopupId, popupId, StringComparison.Ordinal))
                {
                    return config;
                }
            }

            return null;
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
                Debug.LogWarning($"[UIPopupManager] UISettings not found at Resources path '{UISettings.ResourcesPath}'.");
            }

            return _settings;
        }
    }
}
