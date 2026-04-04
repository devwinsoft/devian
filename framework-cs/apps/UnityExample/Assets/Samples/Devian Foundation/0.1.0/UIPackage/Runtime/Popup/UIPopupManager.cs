using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Devian
{
    public sealed class UIPopupManager : AutoSingleton<UIPopupManager>
    {
        private static readonly MethodInfo s_spawnPopupFrameGenericMethod =
            typeof(UIPopupManager).GetMethod(nameof(SpawnPopupFrameGeneric), BindingFlags.NonPublic | BindingFlags.Static);

        private sealed class PopupStackEntry
        {
            public string FrameId;
            public UIPopupFrameBase Frame;
            public Action<PopupCloseReason> OnClosed;
            public PopupFrameState State;
            public PopupCloseReason PendingCloseReason;
        }

        private readonly List<PopupStackEntry> _stack = new List<PopupStackEntry>();
        private readonly Dictionary<UIPopupFrameBase, PopupStackEntry> _entryByFrame = new Dictionary<UIPopupFrameBase, PopupStackEntry>();
        private readonly Dictionary<string, MethodInfo> _spawnMethodByFrameId = new Dictionary<string, MethodInfo>();
        private readonly Dictionary<string, Type> _frameTypeByFrameId = new Dictionary<string, Type>();

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

        public bool Show(UI_POPUP_FRAME_ID frameId, object payload = null, Action<PopupCloseReason> onClosed = null)
        {
            return ShowInternal(frameId, payload, onClosed);
        }

        public bool CloseTop(PopupCloseReason reason = PopupCloseReason.Cancel)
        {
            var topEntry = GetTopEntry();
            if (topEntry == null)
            {
                return false;
            }

            RequestClose(topEntry, reason);
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
                ForceCloseEntry(entry, PopupCloseReason.Cancel);
            }

            _stack.Clear();
            _entryByFrame.Clear();
            RefreshStackState();
        }

        internal void HandleDimClicked()
        {
            var topEntry = GetTopEntry();
            if (topEntry?.Frame == null || !topEntry.Frame.closeOnDimClick)
            {
                return;
            }

            RequestClose(topEntry, PopupCloseReason.Cancel);
        }

        private void Update()
        {
            var topEntry = GetTopEntry();
            if (topEntry?.Frame == null)
            {
                return;
            }

            if (!Input.GetKeyDown(KeyCode.Escape))
            {
                return;
            }

            #if UNITY_ANDROID && !UNITY_EDITOR
            if (!topEntry.Frame.closeOnBack)
            {
                return;
            }

            RequestClose(topEntry, PopupCloseReason.Cancel);
            #else
            if (!topEntry.Frame.closeOnEscape)
            {
                return;
            }

            RequestClose(topEntry, PopupCloseReason.Cancel);
            #endif
        }

        private bool HandleDuplicate(PopupStackEntry duplicateEntry)
        {
            if (duplicateEntry?.Frame == null)
            {
                return true;
            }

            switch (duplicateEntry.Frame.duplicatePolicy)
            {
                case PopupDuplicatePolicy.IgnoreIfShow:
                    return false;

                case PopupDuplicatePolicy.FocusIfShow:
                    FocusEntry(duplicateEntry);
                    return false;

                case PopupDuplicatePolicy.ReplaceIfShow:
                    RequestClose(duplicateEntry, PopupCloseReason.Cancel);
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

        private void RequestClose(PopupStackEntry entry, PopupCloseReason reason)
        {
            if (entry == null || entry.Frame == null || entry.State == PopupFrameState.Closing)
            {
                return;
            }

            entry.PendingCloseReason = reason;
            entry.State = PopupFrameState.Closing;
            RefreshStackState();
            entry.Frame.CloseFromManager(reason);
        }

        private void ForceCloseEntry(PopupStackEntry entry, PopupCloseReason reason)
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

            entry.OnClosed?.Invoke(reason);
        }

        private void HandleFrameShow(UIPopupFrameBase frame)
        {
            if (frame == null || !_entryByFrame.TryGetValue(frame, out var entry))
            {
                return;
            }

            entry.State = PopupFrameState.Show;
            RefreshStackState();
        }

        private void HandleFrameCloseStarted(UIPopupFrameBase frame, PopupCloseReason reason)
        {
            if (frame == null || !_entryByFrame.TryGetValue(frame, out var entry))
            {
                return;
            }

            entry.PendingCloseReason = reason;
            entry.State = PopupFrameState.Closing;
            RefreshStackState();
        }

        private void HandleFrameClosed(UIPopupFrameBase frame, PopupCloseReason reason)
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
            entry.OnClosed?.Invoke(reason);
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

                var allowInput = i == topIndex && entry.State == PopupFrameState.Show;
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
            if (topEntry?.Frame == null)
            {
                panel.ApplyModalState(false, false, false);
                return;
            }

            var blockBehind = topEntry.Frame.blockInputBehind || topEntry.Frame.closeOnDimClick;

            panel.ApplyModalState(
                topEntry.Frame.useDim,
                blockBehind,
                topEntry.Frame.closeOnDimClick);
        }

        private PopupStackEntry FindShowEntry(string frameId)
        {
            for (var i = _stack.Count - 1; i >= 0; i--)
            {
                var entry = _stack[i];
                if (entry == null || entry.FrameId == null)
                {
                    continue;
                }

                if (entry.State == PopupFrameState.Closing)
                {
                    continue;
                }

                if (entry.FrameId == frameId)
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

        private bool ShowInternal(UI_POPUP_FRAME_ID frameId, object payload, Action<PopupCloseReason> onClosed)
        {
            if (frameId == null || !frameId.IsValid)
            {
                Debug.LogWarning("[UIPopupManager] Invalid popup frame id.");
                return false;
            }

            var panel = ResolvePanel();
            if (panel == null)
            {
                Debug.LogWarning("[UIPopupManager] UIPopupPanel is unavailable. Popup request skipped.");
                return false;
            }

            if (!TryGetFrameType(frameId.Value, out var frameType))
            {
                Debug.LogWarning($"[UIPopupManager] Popup frame mapping not found for id '{frameId.Value}'.");
                return false;
            }

            var duplicateEntry = FindShowEntry(frameId.Value);
            if (!HandleDuplicate(duplicateEntry))
            {
                return false;
            }

            UIPopupFrameBase frame;
            try
            {
                frame = SpawnPopupFrame(frameType, frameId, panel.popupRoot);
            }
            catch (Exception e)
            {
                Debug.LogError($"[UIPopupManager] Failed to spawn popup frame '{frameId.Value}': {e.Message}");
                return false;
            }

            if (frame == null)
            {
                Debug.LogError($"[UIPopupManager] Spawn returned null for popup frame '{frameId.Value}'.");
                return false;
            }

            panel.AttachFrame(frame);

            var entry = new PopupStackEntry
            {
                FrameId = frameId.Value,
                Frame = frame,
                OnClosed = onClosed,
                State = PopupFrameState.Showing
            };

            _stack.Add(entry);
            _entryByFrame[frame] = entry;
            RefreshStackState();

            frame.ShowUntyped(payload, HandleFrameShow, HandleFrameCloseStarted, HandleFrameClosed);
            return true;
        }

        private UIPopupFrameBase SpawnPopupFrame(Type frameType, string frameId, Transform parent)
        {
            if (!_spawnMethodByFrameId.TryGetValue(frameId, out var method))
            {
                method = s_spawnPopupFrameGenericMethod.MakeGenericMethod(frameType);
                _spawnMethodByFrameId[frameId] = method;
            }

            try
            {
                return method.Invoke(null, new object[] { frameId, parent }) as UIPopupFrameBase;
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                throw e.InnerException;
            }
        }

        private static UIPopupFrameBase SpawnPopupFrameGeneric<TFrame>(string frameId, Transform parent)
            where TFrame : UIPopupFrameBase, IPoolable
        {
            return BundlePool.Spawn<TFrame>(frameId, parent: parent);
        }

        private bool TryGetFrameType(string frameId, out Type frameType)
        {
            if (_frameTypeByFrameId.TryGetValue(frameId, out frameType))
            {
                return frameType != null;
            }

            try
            {
                var prefab = BundlePoolFactory.Instance.GetPrefab<GameObject>(frameId);
                if (prefab != null)
                {
                    var type = BundlePoolFactory.Instance.GetPoolType(prefab);
                    if (type != null && typeof(UIPopupFrameBase).IsAssignableFrom(type) && !type.IsAbstract)
                    {
                        _frameTypeByFrameId[frameId] = type;
                        frameType = type;
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UIPopupManager] Failed to resolve frame type for '{frameId}': {e.Message}");
            }

            _frameTypeByFrameId[frameId] = null;
            frameType = null;
            return false;
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
