using System;
using UnityEngine;

namespace Devian
{
    public sealed class BundleSettings : ScriptableObject
    {
        // Resources.Load 경로 (정본 SSOT)
        public const string ResourcesPath = "Devian/BundleSettings";

        // 프로젝트 에셋 경로 (정본 SSOT)
        public const string DefaultResourcesAssetPath = "Assets/Resources/Devian/BundleSettings.asset";

        [Serializable]
        public sealed class SettingsEntry
        {
            public string Key;
            public string Value;
        }

        [Header("Bundle")]
        [SerializeField]
        [Tooltip("Clear dependency cache before calculating size (DANGER: use only for testing)")]
        private bool _forceClearDependencyCache;

        [Header("Entries")]
        [SerializeField] private SettingsEntry[] _entries = Array.Empty<SettingsEntry>();

        public bool ForceClearDependencyCache => _forceClearDependencyCache;

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

    }
}
