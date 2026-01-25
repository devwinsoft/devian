// SSOT: skills/devian-upm/30-unity-components/10-asset-manager/SKILL.md
// Devian Unity Asset Manager - Bundle + Editor Find Only
// Resources 기반 로딩 없음

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Devian
{
    /// <summary>
    /// Asset Manager for bundle-based asset loading and editor asset finding.
    /// No Resources support - use AssetBundle or Editor Find* methods only.
    /// </summary>
    public static class AssetManager
    {
        // ====================================================================
        // Bundle Cache
        // ====================================================================

        private static readonly Dictionary<string, AssetBundle> mBundles = new();
        private static readonly Dictionary<Type, Dictionary<string, UnityEngine.Object>> mBundleAssets = new();
        private static readonly Dictionary<string, HashSet<string>> mBundleAssetNamesByBundleKey = new();

        // ====================================================================
        // Bundle Load/Unload
        // ====================================================================

        /// <summary>
        /// Load an AssetBundle from file path and register it with the given key.
        /// </summary>
        /// <param name="key">Unique key to identify this bundle</param>
        /// <param name="bundleFilePath">Full path to the bundle file</param>
        public static IEnumerator LoadBundle(string key, string bundleFilePath)
        {
            if (mBundles.ContainsKey(key))
            {
                Debug.LogWarning($"[AssetManager] Bundle '{key}' already loaded.");
                yield break;
            }

            var request = AssetBundle.LoadFromFileAsync(bundleFilePath);
            yield return request;

            if (request.assetBundle == null)
            {
                Debug.LogError($"[AssetManager] Failed to load bundle from: {bundleFilePath}");
                yield break;
            }

            mBundles[key] = request.assetBundle;
            mBundleAssetNamesByBundleKey[key] = new HashSet<string>();
        }

        /// <summary>
        /// Unload a bundle and remove its cached assets.
        /// </summary>
        /// <param name="key">Bundle key</param>
        /// <param name="unloadAllLoadedObjects">If true, also destroys all loaded objects from the bundle</param>
        public static void UnloadBundle(string key, bool unloadAllLoadedObjects = false)
        {
            if (!mBundles.TryGetValue(key, out var bundle))
            {
                return;
            }

            bundle.Unload(unloadAllLoadedObjects);
            mBundles.Remove(key);

            // Remove cached asset names for this bundle
            if (mBundleAssetNamesByBundleKey.TryGetValue(key, out var assetNames))
            {
                foreach (var assetName in assetNames)
                {
                    foreach (var typeDict in mBundleAssets.Values)
                    {
                        typeDict.Remove(assetName);
                    }
                }
                mBundleAssetNamesByBundleKey.Remove(key);
            }
        }

        // ====================================================================
        // Bundle Asset Loading
        // ====================================================================

        /// <summary>
        /// Load all assets of type T from a loaded bundle and cache them.
        /// </summary>
        /// <typeparam name="T">Asset type</typeparam>
        /// <param name="key">Bundle key (must be loaded via LoadBundle first)</param>
        public static IEnumerator LoadBundleAssets<T>(string key) where T : UnityEngine.Object
        {
            if (!mBundles.TryGetValue(key, out var bundle))
            {
                Debug.LogError($"[AssetManager] Bundle '{key}' not loaded.");
                yield break;
            }

            var request = bundle.LoadAllAssetsAsync<T>();
            yield return request;

            var type = typeof(T);
            if (!mBundleAssets.TryGetValue(type, out var typeDict))
            {
                typeDict = new Dictionary<string, UnityEngine.Object>();
                mBundleAssets[type] = typeDict;
            }

            if (!mBundleAssetNamesByBundleKey.TryGetValue(key, out var bundleAssetNames))
            {
                bundleAssetNames = new HashSet<string>();
                mBundleAssetNamesByBundleKey[key] = bundleAssetNames;
            }

            foreach (var asset in request.allAssets)
            {
                var assetKey = asset.name.ToLowerInvariant();
                typeDict[assetKey] = asset;
                bundleAssetNames.Add(assetKey);
            }
        }

        /// <summary>
        /// Get a cached asset by filename (extension removed, case-insensitive).
        /// Returns null if not found in bundle cache.
        /// </summary>
        /// <typeparam name="T">Asset type</typeparam>
        /// <param name="fileName">Asset filename (with or without extension)</param>
        /// <returns>Asset or null if not found</returns>
        public static T? GetAsset<T>(string fileName) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(fileName))
                return null;

            // Remove extension if present
            var dotIndex = fileName.LastIndexOf('.');
            var nameWithoutExt = dotIndex >= 0 ? fileName.Substring(0, dotIndex) : fileName;
            var assetKey = nameWithoutExt.ToLowerInvariant();

            var type = typeof(T);
            if (mBundleAssets.TryGetValue(type, out var typeDict))
            {
                if (typeDict.TryGetValue(assetKey, out var asset))
                {
                    return asset as T;
                }
            }

            return null;
        }

        /// <summary>
        /// Clear all cached bundles and assets.
        /// </summary>
        public static void ClearAll()
        {
            foreach (var bundle in mBundles.Values)
            {
                bundle.Unload(false);
            }
            mBundles.Clear();
            mBundleAssets.Clear();
            mBundleAssetNamesByBundleKey.Clear();
        }

        // ====================================================================
        // Editor-Only Find Methods
        // ====================================================================

#if UNITY_EDITOR
        private static readonly string[] DefaultSearchDirs = { "Assets", "Packages" };

        /// <summary>
        /// Find assets by filename in the project (Editor only).
        /// </summary>
        /// <typeparam name="T">Asset type</typeparam>
        /// <param name="fileName">Filename to search (without path)</param>
        /// <param name="searchDirs">Directories to search in (defaults to Assets, Packages)</param>
        /// <returns>Array of matching assets</returns>
        public static T[] FindAssets<T>(string fileName, params string[] searchDirs) where T : UnityEngine.Object
        {
            var dirs = (searchDirs == null || searchDirs.Length == 0) ? DefaultSearchDirs : searchDirs;
            var typeName = typeof(T).Name;
            var guids = AssetDatabase.FindAssets($"t:{typeName} {fileName}", dirs);

            var results = new List<T>();
            var targetName = System.IO.Path.GetFileNameWithoutExtension(fileName);

            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var candidateName = System.IO.Path.GetFileNameWithoutExtension(assetPath);

                // Exact filename match (case-insensitive)
                if (!string.Equals(targetName, candidateName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
                if (asset != null)
                {
                    results.Add(asset);
                }
            }

            return results.ToArray();
        }

        /// <summary>
        /// Find all prefabs in the project (Editor only).
        /// </summary>
        /// <param name="searchDirs">Directories to search in (defaults to Assets, Packages)</param>
        /// <returns>Array of prefab GameObjects</returns>
        public static GameObject[] FindPrefabs(params string[] searchDirs)
        {
            var dirs = (searchDirs == null || searchDirs.Length == 0) ? DefaultSearchDirs : searchDirs;
            var guids = AssetDatabase.FindAssets("t:Prefab", dirs);

            var results = new List<GameObject>();

            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab != null)
                {
                    results.Add(prefab);
                }
            }

            return results.ToArray();
        }

        /// <summary>
        /// Find prefabs with a specific component (Editor only).
        /// </summary>
        /// <typeparam name="T">Component type</typeparam>
        /// <param name="searchDirs">Directories to search in (defaults to Assets, Packages)</param>
        /// <returns>Array of prefab GameObjects with the component</returns>
        public static GameObject[] FindPrefabs<T>(params string[] searchDirs) where T : Component
        {
            var dirs = (searchDirs == null || searchDirs.Length == 0) ? DefaultSearchDirs : searchDirs;
            var guids = AssetDatabase.FindAssets("t:Prefab", dirs);

            var results = new List<GameObject>();

            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab != null && prefab.GetComponent<T>() != null)
                {
                    results.Add(prefab);
                }
            }

            return results.ToArray();
        }
#endif
    }
}
