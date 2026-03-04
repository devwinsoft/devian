using System;
using UnityEngine;

namespace Devian
{
    public sealed class DevianSettings : ScriptableObject
    {
        // Resources.Load 경로 (정본 SSOT)
        public const string ResourcesPath = "Devian/DevianSettings";

        // 프로젝트 에셋 경로 (정본 SSOT)
        public const string DefaultResourcesAssetPath = "Assets/Resources/Devian/DevianSettings.asset";

        // 마이그레이션용 레거시 경로 (deprecated)
        public const string LegacyAssetPath = "Assets/Settings/DevianSettings.asset";

        // Default PlayerPrefs key prefix (must end with '.')
        public const string DefaultPlayerPrefsPrefix = "devian.game.";

        [Serializable]
        public sealed class SettingsEntry
        {
            public string Key;
            public string Value;
        }

        [SerializeField] private SettingsEntry[] _entries = Array.Empty<SettingsEntry>();
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

        /// <summary>
        /// Returns the value for the given key, or empty string if not found.
        /// </summary>
        public string GetEntry(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            for (var i = 0; i < _entries.Length; i++)
            {
                var e = _entries[i];
                if (e == null) continue;

                if (string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return e.Value ?? string.Empty;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Ensures a key-value entry exists. Used by editor menu for seed/auto-repair.
        /// </summary>
        public void EnsureEntry(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (value == null)
            {
                value = string.Empty;
            }

            if (_entries == null)
            {
                _entries = Array.Empty<SettingsEntry>();
            }

            for (var i = 0; i < _entries.Length; i++)
            {
                var e = _entries[i];
                if (e == null) continue;

                if (string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    e.Value = value;
                    return;
                }
            }

            var next = new SettingsEntry[_entries.Length + 1];
            Array.Copy(_entries, next, _entries.Length);
            next[_entries.Length] = new SettingsEntry
            {
                Key = key,
                Value = value
            };
            _entries = next;
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
