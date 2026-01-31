using System;
using UnityEngine;

namespace Devian
{
    public sealed class DevianSettings : ScriptableObject
    {
        // Fixed default path (project asset, not inside package)
        public const string DefaultAssetPath = "Assets/Settings/DevianSettings.asset";

        // Default PlayerPrefs key prefix (must end with '.')
        public const string DefaultPlayerPrefsPrefix = "devian.game.";

        [Serializable]
        public sealed class AssetIdEntry
        {
            public string GroupKey;   // e.g. "EFFECT"
            public string SearchDir;  // e.g. "Assets/Bundles/Effects"
        }

        [SerializeField] private AssetIdEntry[] _assetId = Array.Empty<AssetIdEntry>();
        [SerializeField] private string _playerPrefsPrefix = DefaultPlayerPrefsPrefix;

        /// <summary>
        /// Gets the PlayerPrefs key prefix. Always ends with '.'.
        /// Returns DefaultPlayerPrefsPrefix if empty/whitespace.
        /// </summary>
        public string PlayerPrefsPrefix
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_playerPrefsPrefix))
                {
                    return DefaultPlayerPrefsPrefix;
                }

                // 규약: 반드시 '.'로 끝나야 함
                if (_playerPrefsPrefix.EndsWith(".", StringComparison.Ordinal))
                {
                    return _playerPrefsPrefix;
                }

                return _playerPrefsPrefix + ".";
            }
        }

        /// <summary>
        /// Combines prefix with a relative key. If relativeKey already starts with prefix, returns as-is.
        /// </summary>
        public string ToPlayerPrefsKey(string relativeKey)
        {
            if (string.IsNullOrWhiteSpace(relativeKey))
            {
                return PlayerPrefsPrefix;
            }

            // 이미 prefix로 시작하면 그대로 반환
            if (relativeKey.StartsWith(PlayerPrefsPrefix, StringComparison.Ordinal))
            {
                return relativeKey;
            }

            if (relativeKey.StartsWith(".", StringComparison.Ordinal))
            {
                relativeKey = relativeKey.Substring(1);
            }

            return PlayerPrefsPrefix + relativeKey;
        }

        public string GetAssetIdSearchDir(string groupKey)
        {
            if (string.IsNullOrWhiteSpace(groupKey))
            {
                return "Assets";
            }

            for (var i = 0; i < _assetId.Length; i++)
            {
                var e = _assetId[i];
                if (e == null) continue;

                if (string.Equals(e.GroupKey, groupKey, StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(e.SearchDir))
                    {
                        return e.SearchDir;
                    }
                    return "Assets";
                }
            }

            return "Assets";
        }

        // Used by editor menu to seed defaults when creating a new asset.
        public void EnsureAssetId(string groupKey, string searchDir)
        {
            if (string.IsNullOrWhiteSpace(groupKey))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(searchDir))
            {
                searchDir = "Assets";
            }

            if (_assetId == null)
            {
                _assetId = Array.Empty<AssetIdEntry>();
            }

            for (var i = 0; i < _assetId.Length; i++)
            {
                var e = _assetId[i];
                if (e == null) continue;

                if (string.Equals(e.GroupKey, groupKey, StringComparison.OrdinalIgnoreCase))
                {
                    e.SearchDir = searchDir;
                    return;
                }
            }

            var next = new AssetIdEntry[_assetId.Length + 1];
            Array.Copy(_assetId, next, _assetId.Length);
            next[_assetId.Length] = new AssetIdEntry
            {
                GroupKey = groupKey,
                SearchDir = searchDir
            };
            _assetId = next;
        }

        /// <summary>
        /// Ensures playerPrefsPrefix is set. Used by editor menu for seed/auto-repair.
        /// </summary>
        public void EnsurePlayerPrefsPrefix(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                prefix = DefaultPlayerPrefsPrefix;
            }

            if (!prefix.EndsWith(".", StringComparison.Ordinal))
            {
                prefix += ".";
            }

            if (string.Equals(_playerPrefsPrefix, prefix, StringComparison.Ordinal))
            {
                return;
            }

            _playerPrefsPrefix = prefix;
        }
    }
}
