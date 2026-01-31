using System;
using UnityEngine;

namespace Devian
{
    public sealed class DevianSettings : ScriptableObject
    {
        // Fixed default path (project asset, not inside package)
        public const string DefaultAssetPath = "Assets/Settings/DevianSettings.asset";

        [Serializable]
        public sealed class AssetIdEntry
        {
            public string GroupKey;   // e.g. "EFFECT"
            public string SearchDir;  // e.g. "Assets/Bundles/Effects"
        }

        [SerializeField] private AssetIdEntry[] _assetId = Array.Empty<AssetIdEntry>();

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
    }
}
