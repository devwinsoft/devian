using System;
using UnityEngine;

namespace Devian
{
    [Serializable]
    public sealed class UIAssetSearchEntry
    {
        public string Key;
        public string SearchDir;
    }

    [CreateAssetMenu(fileName = "UISettings", menuName = "Devian/UI/UI Settings")]
    public sealed class UISettings : ScriptableObject
    {
        public const string ResourcesPath = "Devian/UISettings";
        public const string DefaultResourcesAssetPath = "Assets/Resources/Devian/UISettings.asset";

        // ── UI ──
        [Header("UI")]
        [SerializeField] private string _uiAddressablesKey = "ui";
        [SerializeField] private UIAssetSearchEntry[] _assetSearchEntries;

        public string UIAddressablesKey => _uiAddressablesKey;

        public string GetSearchDir(string key)
        {
            if (_assetSearchEntries == null) return null;
            for (var i = 0; i < _assetSearchEntries.Length; i++)
            {
                var entry = _assetSearchEntries[i];
                if (entry != null && string.Equals(entry.Key, key, StringComparison.Ordinal))
                    return entry.SearchDir;
            }
            return null;
        }

        // ── Toast ──
        [Header("Toast")]
        [SerializeField] private UI_TOAST_CANVAS_ID _toastCanvasId;
        [SerializeField] private ToastGroupConfig[] _groupConfigs;

        public UI_TOAST_CANVAS_ID ToastCanvasId => _toastCanvasId;
        public ToastGroupConfig[] GroupConfigs => _groupConfigs;

        // ── Popup ──
        [Header("Popup")]
        [SerializeField] private UI_POPUP_CANVAS_ID _popupCanvasId;
        [SerializeField] private UIPopupFrameMapEntry[] _popupFrameMappings;
        [SerializeField] private Color _dimColor = Color.black;
        [SerializeField, Range(0f, 1f)] private float _dimAlpha = UIPopupDefaults.DefaultDimAlpha;

        public UI_POPUP_CANVAS_ID PopupCanvasId => _popupCanvasId;
        public UIPopupFrameMapEntry[] PopupFrameMappings => _popupFrameMappings;
        public Color DimColor => _dimColor;
        public float DimAlpha => _dimAlpha;
    }
}
