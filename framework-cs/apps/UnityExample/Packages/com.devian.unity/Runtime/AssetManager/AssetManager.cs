// SSOT: skills/devian-unity/30-unity-components/10-asset-manager/SKILL.md
// Devian Unity Asset Manager - Addressables 기반 로딩/캐시 + Resources(옵션) + Editor Find
// DownloadManager와 연동: DownloadManager가 다운로드 → AssetManager가 로딩/캐시

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Devian
{
    /// <summary>
    /// Asset Manager for Addressables-based asset loading and caching.
    /// - Addressables: LoadBundleAsset(s) / UnloadBundleAssets
    /// - Resources(옵션): LoadResourceAsset(s) / UnloadResourceAsset(s)
    /// - Editor: FindAssets / FindPrefabs
    /// </summary>
    public static class AssetManager
    {
        // ====================================================================
        // Internal Data Structures
        // ====================================================================

        private sealed class BundleData
        {
            public readonly string Key;
            public AsyncOperationHandle Handle;
            public readonly HashSet<string> Names = new();

            public BundleData(string key)
            {
                Key = key;
            }
        }

        private sealed class ResData
        {
            public readonly string Path;
            public readonly UnityEngine.Object Asset;

            public ResData(string path, UnityEngine.Object asset)
            {
                Path = path;
                Asset = asset;
            }
        }

        // ====================================================================
        // Bundle Cache (Addressables)
        // ====================================================================

        private static readonly Dictionary<string, BundleData> mBundles = new();
        private static readonly Dictionary<Type, Dictionary<string, UnityEngine.Object>> mBundleAssets = new();

        // ====================================================================
        // Resource Cache (Resources)
        // ====================================================================

        private static readonly Dictionary<Type, Dictionary<string, ResData>> mResourceAssets = new();

        // ====================================================================
        // Addressables Load/Unload
        // ====================================================================

        /// <summary>
        /// Load a single asset by Addressables key.
        /// </summary>
        /// <typeparam name="T">Asset type</typeparam>
        /// <param name="key">Addressables key or address</param>
        public static IEnumerator LoadBundleAsset<T>(string key) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("[AssetManager] LoadBundleAsset: key is null or empty.");
                yield break;
            }

            // Already loaded? Prevent handle leak
            if (mBundles.ContainsKey(key))
            {
                Debug.LogWarning($"[AssetManager] Bundle '{key}' already loaded.");
                yield break;
            }

            var handle = Addressables.LoadAssetAsync<T>(key);
            yield return handle;

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"[AssetManager] LoadBundleAsset failed for key '{key}': {handle.OperationException?.Message}");
                Addressables.Release(handle);
                yield break;
            }

            var asset = handle.Result;
            if (asset == null)
            {
                Addressables.Release(handle);
                yield break;
            }

            // Register to cache
            var assetKey = NormalizeAssetName(asset.name);
            
            // Skip if name starts with @
            if (asset.name.StartsWith("@"))
            {
                Addressables.Release(handle);
                yield break;
            }

            var type = typeof(T);
            if (!mBundleAssets.TryGetValue(type, out var typeDict))
            {
                typeDict = new Dictionary<string, UnityEngine.Object>();
                mBundleAssets[type] = typeDict;
            }

            if (typeDict.ContainsKey(assetKey))
            {
                Debug.LogError($"[AssetManager] Duplicate asset name '{assetKey}' for type {type.Name}. Ignoring.");
                Addressables.Release(handle);
                yield break;
            }

            typeDict[assetKey] = asset;

            // Track bundle data
            if (!mBundles.TryGetValue(key, out var bundleData))
            {
                bundleData = new BundleData(key) { Handle = handle };
                mBundles[key] = bundleData;
            }
            bundleData.Names.Add(assetKey);
        }

        /// <summary>
        /// Load all assets of type T by Addressables label/key.
        /// </summary>
        /// <typeparam name="T">Asset type</typeparam>
        /// <param name="key">Addressables label or key</param>
        public static IEnumerator LoadBundleAssets<T>(string key) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("[AssetManager] LoadBundleAssets: key is null or empty.");
                yield break;
            }

            // Already loaded?
            if (mBundles.ContainsKey(key))
            {
                Debug.LogWarning($"[AssetManager] Bundle '{key}' already loaded.");
                yield break;
            }

            var bundleData = new BundleData(key);
            var type = typeof(T);

            if (!mBundleAssets.TryGetValue(type, out var typeDict))
            {
                typeDict = new Dictionary<string, UnityEngine.Object>();
                mBundleAssets[type] = typeDict;
            }

            var handle = Addressables.LoadAssetsAsync<T>(
                key,
                asset =>
                {
                    if (asset == null) return;

                    // Skip if name starts with @
                    if (asset.name.StartsWith("@")) return;

                    var assetKey = NormalizeAssetName(asset.name);

                    if (typeDict.ContainsKey(assetKey))
                    {
                        Debug.LogError($"[AssetManager] Duplicate asset name '{assetKey}' for type {type.Name}. Ignoring.");
                        return;
                    }

                    typeDict[assetKey] = asset;
                    bundleData.Names.Add(assetKey);
                }
            );

            yield return handle;

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"[AssetManager] LoadBundleAssets failed for key '{key}': {handle.OperationException?.Message}");
                Addressables.Release(handle);
                yield break;
            }

            bundleData.Handle = handle;
            mBundles[key] = bundleData;
        }

        /// <summary>
        /// Load assets by label with language filter (Intersection merge).
        /// </summary>
        /// <typeparam name="T">Asset type</typeparam>
        /// <param name="key">Addressables label</param>
        /// <param name="lang">Language filter</param>
        public static IEnumerator LoadBundleAssets<T>(string key, SystemLanguage lang) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("[AssetManager] LoadBundleAssets: key is null or empty.");
                yield break;
            }

            // Already loaded?
            if (mBundles.ContainsKey(key))
            {
                Debug.LogWarning($"[AssetManager] Bundle '{key}' already loaded.");
                yield break;
            }

            var bundleData = new BundleData(key);
            var type = typeof(T);

            if (!mBundleAssets.TryGetValue(type, out var typeDict))
            {
                typeDict = new Dictionary<string, UnityEngine.Object>();
                mBundleAssets[type] = typeDict;
            }

            var keys = new List<object> { key, lang.ToString() };

            var handle = Addressables.LoadAssetsAsync<T>(
                keys,
                asset =>
                {
                    if (asset == null) return;

                    // Skip if name starts with @
                    if (asset.name.StartsWith("@")) return;

                    var assetKey = NormalizeAssetName(asset.name);

                    if (typeDict.ContainsKey(assetKey))
                    {
                        Debug.LogError($"[AssetManager] Duplicate asset name '{assetKey}' for type {type.Name}. Ignoring.");
                        return;
                    }

                    typeDict[assetKey] = asset;
                    bundleData.Names.Add(assetKey);
                },
                Addressables.MergeMode.Intersection
            );

            yield return handle;

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"[AssetManager] LoadBundleAssets failed for key '{key}': {handle.OperationException?.Message}");
                Addressables.Release(handle);
                yield break;
            }

            bundleData.Handle = handle;
            mBundles[key] = bundleData;
        }

        /// <summary>
        /// Unload assets loaded by the given key and release the handle.
        /// </summary>
        /// <param name="key">Addressables key/label used in LoadBundleAssets</param>
        /// <returns>List of unloaded asset names</returns>
        public static IEnumerable<string> UnloadBundleAssets(string key)
        {
            var unloadedNames = new List<string>();

            if (!mBundles.TryGetValue(key, out var bundleData))
            {
                return unloadedNames;
            }

            // Remove from asset cache
            foreach (var assetName in bundleData.Names)
            {
                foreach (var typeDict in mBundleAssets.Values)
                {
                    if (typeDict.Remove(assetName))
                    {
                        unloadedNames.Add(assetName);
                    }
                }
            }

            // Release handle
            if (bundleData.Handle.IsValid())
            {
                Addressables.Release(bundleData.Handle);
            }

            mBundles.Remove(key);

            return unloadedNames;
        }

        // ====================================================================
        // Cache Access (즉시 반환)
        // ====================================================================

        /// <summary>
        /// Get a cached asset by filename (extension removed, case-insensitive).
        /// Search order: Bundle cache → Resource cache
        /// </summary>
        /// <typeparam name="T">Asset type</typeparam>
        /// <param name="fileName">Asset filename (with or without extension)</param>
        /// <returns>Asset or null if not found</returns>
        public static T? GetAsset<T>(string fileName) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(fileName))
                return null;

            var assetKey = NormalizeAssetName(fileName);
            var type = typeof(T);

            // 1. Bundle cache
            if (mBundleAssets.TryGetValue(type, out var bundleDict))
            {
                if (bundleDict.TryGetValue(assetKey, out var bundleAsset))
                {
                    return bundleAsset as T;
                }
            }

            // 2. Resource cache
            if (mResourceAssets.TryGetValue(type, out var resDict))
            {
                if (resDict.TryGetValue(assetKey, out var resData))
                {
                    return resData.Asset as T;
                }
            }

            return null;
        }

        /// <summary>
        /// Alias for GetAsset (backward compatibility with existing code style).
        /// </summary>
        /// <typeparam name="T">Asset type</typeparam>
        /// <param name="fileName">Asset filename</param>
        /// <returns>Asset or null if not found</returns>
        public static T? LoadAsset<T>(string fileName) where T : UnityEngine.Object
        {
            return GetAsset<T>(fileName);
        }

        // ====================================================================
        // Resources (옵션)
        // ====================================================================

        /// <summary>
        /// Load a single asset from Resources folder.
        /// </summary>
        /// <typeparam name="T">Asset type</typeparam>
        /// <param name="filePath">Path relative to Resources folder (without extension)</param>
        /// <param name="lang">Optional language suffix</param>
        /// <returns>Loaded asset or null</returns>
        public static T? LoadResourceAsset<T>(string filePath, SystemLanguage lang = SystemLanguage.Unknown) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(filePath))
                return null;

            var path = lang != SystemLanguage.Unknown
                ? $"{filePath}_{lang}"
                : filePath;

            var asset = Resources.Load<T>(path);
            if (asset == null)
            {
                Debug.LogWarning($"[AssetManager] LoadResourceAsset: Failed to load '{path}'");
                return null;
            }

            // Register to cache
            var assetKey = NormalizeAssetName(asset.name);
            var type = typeof(T);

            if (!mResourceAssets.TryGetValue(type, out var typeDict))
            {
                typeDict = new Dictionary<string, ResData>();
                mResourceAssets[type] = typeDict;
            }

            if (!typeDict.ContainsKey(assetKey))
            {
                typeDict[assetKey] = new ResData(path, asset);
            }

            return asset;
        }

        /// <summary>
        /// Load all assets of type T from a Resources subdirectory.
        /// </summary>
        /// <typeparam name="T">Asset type</typeparam>
        /// <param name="searchDir">Directory relative to Resources folder</param>
        /// <param name="lang">Optional language suffix for directory</param>
        /// <returns>Array of loaded assets</returns>
        public static T[] LoadResourceAssets<T>(string searchDir, SystemLanguage lang = SystemLanguage.Unknown) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(searchDir))
                return Array.Empty<T>();

            var dir = lang != SystemLanguage.Unknown
                ? $"{searchDir}_{lang}"
                : searchDir;

            var assets = Resources.LoadAll<T>(dir);
            if (assets == null || assets.Length == 0)
            {
                return Array.Empty<T>();
            }

            var type = typeof(T);
            if (!mResourceAssets.TryGetValue(type, out var typeDict))
            {
                typeDict = new Dictionary<string, ResData>();
                mResourceAssets[type] = typeDict;
            }

            foreach (var asset in assets)
            {
                var assetKey = NormalizeAssetName(asset.name);
                if (!typeDict.ContainsKey(assetKey))
                {
                    typeDict[assetKey] = new ResData(dir, asset);
                }
            }

            return assets;
        }

        /// <summary>
        /// Unload a single resource asset from cache.
        /// </summary>
        /// <typeparam name="T">Asset type</typeparam>
        /// <param name="fileName">Asset filename</param>
        public static void UnloadResourceAsset<T>(string fileName) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(fileName))
                return;

            var assetKey = NormalizeAssetName(fileName);
            var type = typeof(T);

            if (mResourceAssets.TryGetValue(type, out var typeDict))
            {
                if (typeDict.TryGetValue(assetKey, out var resData))
                {
                    Resources.UnloadAsset(resData.Asset);
                    typeDict.Remove(assetKey);
                }
            }
        }

        /// <summary>
        /// Unload all resource assets loaded from a specific directory.
        /// </summary>
        /// <typeparam name="T">Asset type</typeparam>
        /// <param name="searchDir">Directory that was used in LoadResourceAssets</param>
        /// <returns>List of unloaded asset names</returns>
        public static IEnumerable<string> UnloadResourceAssets<T>(string searchDir) where T : UnityEngine.Object
        {
            var unloadedNames = new List<string>();
            var type = typeof(T);

            if (!mResourceAssets.TryGetValue(type, out var typeDict))
            {
                return unloadedNames;
            }

            var toRemove = new List<string>();
            foreach (var kvp in typeDict)
            {
                if (kvp.Value.Path.StartsWith(searchDir, StringComparison.OrdinalIgnoreCase))
                {
                    Resources.UnloadAsset(kvp.Value.Asset);
                    toRemove.Add(kvp.Key);
                    unloadedNames.Add(kvp.Key);
                }
            }

            foreach (var key in toRemove)
            {
                typeDict.Remove(key);
            }

            return unloadedNames;
        }

        // ====================================================================
        // Cleanup
        // ====================================================================

        /// <summary>
        /// Clear all cached bundles and resources.
        /// </summary>
        public static void ClearAll()
        {
            // Release all Addressables handles
            foreach (var bundleData in mBundles.Values)
            {
                if (bundleData.Handle.IsValid())
                {
                    Addressables.Release(bundleData.Handle);
                }
            }
            mBundles.Clear();
            mBundleAssets.Clear();

            // Unload all Resources
            foreach (var typeDict in mResourceAssets.Values)
            {
                foreach (var resData in typeDict.Values)
                {
                    if (resData.Asset != null)
                    {
                        Resources.UnloadAsset(resData.Asset);
                    }
                }
            }
            mResourceAssets.Clear();
        }

        // ====================================================================
        // Helper Methods
        // ====================================================================

        /// <summary>
        /// Normalize asset name: remove extension, lowercase.
        /// </summary>
        private static string NormalizeAssetName(string name)
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(name);
            return nameWithoutExt.ToLowerInvariant();
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
            var targetName = Path.GetFileNameWithoutExtension(fileName);

            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var candidateName = Path.GetFileNameWithoutExtension(assetPath);

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
