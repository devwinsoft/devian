// SSOT: skills/devian-unity/30-unity-components/14-table-manager/SKILL.md
// Devian Unity TableManager - Raw data loading for TB_/ST_
// 
// Key Design:
// - AutoSingleton-based (auto-created on first Instance access)
// - Addressables key is NOT enforced by Devian (project policy)
// - Cache key is (format, fileName) where fileName = TextAsset.name
// - If fileName is {TableName}@{Description}, baseName = part before @
// - TB/ST auto-insert via RegisterTbLoader/RegisterStLoader registry
// - ST: 1 language per baseName (language change requires reload)
// - Editor guard: Play Mode only
// - LoadAssetsAsync for multiple asset loading with SharedHandle(refcount)

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Devian
{
    /// <summary>
    /// Table Manager for loading raw table data.
    /// - LoadTablesAsync: TB_ loading with auto-insert (multiple assets)
    /// - LoadStringsAsync: ST_ loading with auto-insert (multiple assets + language intersection)
    /// 
    /// AutoSingleton-based: Auto-created on first Instance access.
    /// Runtime-only: Editor (non-Play Mode) calls will fail.
    /// </summary>
    public sealed class TableManager : AutoSingleton<TableManager>
    {
        // ====================================================================
        // TB Loader Registry
        // ====================================================================

        /// <summary>
        /// TB loader delegate: (format, ndjsonText, pb64Binary) => insert to TB_{baseName}
        /// </summary>
        public delegate void TbLoaderDelegate(TableFormat format, string? ndjsonText, byte[]? pb64Binary);

        private readonly Dictionary<string, TbLoaderDelegate> mTbLoaders = new();

        /// <summary>
        /// Register a TB loader for auto-insert when loading tables.
        /// </summary>
        /// <param name="baseTableName">Base table name (e.g., "Monsters" for TB_Monsters)</param>
        /// <param name="loader">Loader delegate to insert data into TB_{baseTableName}</param>
        /// <exception cref="InvalidOperationException">Thrown if baseTableName is already registered</exception>
        public void RegisterTbLoader(string baseTableName, TbLoaderDelegate loader)
        {
            if (mTbLoaders.ContainsKey(baseTableName))
            {
                throw new InvalidOperationException(
                    $"[TableManager] TB loader for '{baseTableName}' is already registered. " +
                    "Duplicate registration is not allowed.");
            }
            mTbLoaders[baseTableName] = loader;
        }

        /// <summary>
        /// Check if a TB loader is registered.
        /// </summary>
        public bool IsTbLoaderRegistered(string baseTableName) => mTbLoaders.ContainsKey(baseTableName);

        // ====================================================================
        // ST Loader Registry
        // ====================================================================

        /// <summary>
        /// ST loader delegate: (format, language, ndjsonText, pb64Text) => insert to ST_{baseName}
        /// </summary>
        public delegate void StLoaderDelegate(TableFormat format, SystemLanguage language, string? ndjsonText, string? pb64Text);

        private readonly Dictionary<string, StLoaderDelegate> mStLoaders = new();

        /// <summary>
        /// Track active language per ST baseName.
        /// Policy: 1 language per baseName, change requires reload.
        /// </summary>
        private readonly Dictionary<string, SystemLanguage> mStActiveLanguages = new();

        /// <summary>
        /// Register a ST loader for auto-insert when loading strings.
        /// </summary>
        /// <param name="baseTableName">Base table name (e.g., "UIText" for ST_UIText)</param>
        /// <param name="loader">Loader delegate to insert data into ST_{baseTableName}</param>
        /// <exception cref="InvalidOperationException">Thrown if baseTableName is already registered</exception>
        public void RegisterStLoader(string baseTableName, StLoaderDelegate loader)
        {
            if (mStLoaders.ContainsKey(baseTableName))
            {
                throw new InvalidOperationException(
                    $"[TableManager] ST loader for '{baseTableName}' is already registered. " +
                    "Duplicate registration is not allowed.");
            }
            mStLoaders[baseTableName] = loader;
        }

        /// <summary>
        /// Check if a ST loader is registered.
        /// </summary>
        public bool IsStLoaderRegistered(string baseTableName) => mStLoaders.ContainsKey(baseTableName);

        /// <summary>
        /// Get the active language for a ST baseName, or null if not loaded.
        /// </summary>
        public SystemLanguage? GetStActiveLanguage(string baseTableName)
        {
            return mStActiveLanguages.TryGetValue(baseTableName, out var lang) ? lang : null;
        }

        // ====================================================================
        // Cache with SharedHandle (refcount)
        // ====================================================================

        private readonly struct CacheKey : IEquatable<CacheKey>
        {
            public readonly TableFormat Format;
            public readonly string FileName;

            public CacheKey(TableFormat format, string fileName)
            {
                Format = format;
                FileName = fileName;
            }

            public bool Equals(CacheKey other) => Format == other.Format && FileName == other.FileName;
            public override bool Equals(object? obj) => obj is CacheKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Format, FileName);
        }

        /// <summary>
        /// SharedHandle for LoadAssetsAsync: one handle shared by multiple cache entries.
        /// RefCount tracks how many CachedData entries reference this handle.
        /// Release only when RefCount reaches 0.
        /// </summary>
        private sealed class SharedHandle
        {
            public AsyncOperationHandle Handle;
            public int RefCount;
        }

        private sealed class CachedData
        {
            public SharedHandle Shared = null!;  // Must be assigned, never store Handle directly
            public string? NdjsonText;
            public byte[]? Pb64Binary;
        }

        private readonly Dictionary<CacheKey, CachedData> mCache = new();

        /// <summary>
        /// Safely release SharedHandle: decrement RefCount, release only when 0.
        /// </summary>
        private void ReleaseShared(CachedData data)
        {
            if (data.Shared == null) return;
            
            data.Shared.RefCount--;
            if (data.Shared.RefCount <= 0 && data.Shared.Handle.IsValid())
            {
                Addressables.Release(data.Shared.Handle);
            }
        }

        // ====================================================================
        // LoadTablesAsync (TB_ loading with auto-insert, multiple assets)
        // ====================================================================

        /// <summary>
        /// Load table data and auto-insert to TB_{baseName}.
        /// Uses LoadAssetsAsync to load multiple assets at once.
        /// Runtime-only: Fails in Editor (non-Play Mode).
        /// </summary>
        /// <param name="key">Addressables key to load TextAssets</param>
        /// <param name="format">Json or Pb64</param>
        /// <param name="onError">Error callback (per-asset errors, does not stop loading)</param>
        public IEnumerator LoadTablesAsync(
            string key,
            TableFormat format,
            Action<string>? onError = null)
        {
            // Editor guard: Play Mode only
#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlaying)
            {
                onError?.Invoke("[TableManager] Runtime-only. Enter Play Mode.");
                yield break;
            }
#endif

            // Load multiple assets via Addressables
            var loadHandle = Addressables.LoadAssetsAsync<TextAsset>(key, null);
            yield return loadHandle;

            if (loadHandle.Status != AsyncOperationStatus.Succeeded)
            {
                onError?.Invoke($"[TableManager] Failed to load '{key}': {loadHandle.OperationException?.Message}");
                Addressables.Release(loadHandle);
                yield break;
            }

            var assets = loadHandle.Result;
            if (assets == null || assets.Count == 0)
            {
                onError?.Invoke($"[TableManager] No TextAssets found for '{key}'");
                Addressables.Release(loadHandle);
                yield break;
            }

            // Create shared handle with initial RefCount = 0
            var shared = new SharedHandle { Handle = loadHandle, RefCount = 0 };

            // Process each asset
            foreach (var textAsset in assets)
            {
                if (textAsset == null)
                {
                    onError?.Invoke($"[TableManager] Null TextAsset in results for '{key}'");
                    continue;
                }

                var fileName = textAsset.name;
                var cacheKey = new CacheKey(format, fileName);

                // Already cached? Skip (do NOT increment RefCount)
                if (mCache.ContainsKey(cacheKey))
                {
                    continue;
                }

                // Check TB loader registration
                var baseName = ExtractBaseName(fileName);
                if (!mTbLoaders.TryGetValue(baseName, out var tbLoader))
                {
                    onError?.Invoke($"[TableManager] No TB loader registered for '{baseName}'. " +
                        $"Call RegisterTbLoader(\"{baseName}\", ...) first.");
                    continue;
                }

                // Parse data and insert to TB
                var cachedData = new CachedData { Shared = shared };

                try
                {
                    if (format == TableFormat.Json)
                    {
                        var ndjsonText = textAsset.text;
                        cachedData.NdjsonText = ndjsonText;
                        tbLoader(format, ndjsonText, null);
                    }
                    else // Pb64
                    {
                        var pb64Binary = Pb64Loader.LoadFromBase64(textAsset.text);
                        cachedData.Pb64Binary = pb64Binary;
                        tbLoader(format, null, pb64Binary);
                    }
                }
                catch (Exception ex)
                {
                    onError?.Invoke($"[TableManager] Parse/insert error for '{fileName}': {ex.Message}");
                    continue;
                }

                // Success: cache and increment RefCount
                mCache[cacheKey] = cachedData;
                shared.RefCount++;
            }

            // If all assets were skipped, release the handle immediately
            if (shared.RefCount == 0)
            {
                Addressables.Release(loadHandle);
            }
        }

        // ====================================================================
        // LoadStringsAsync (ST_ loading with auto-insert, multiple assets + language)
        // ====================================================================

        /// <summary>
        /// Load string table data and auto-insert to ST_{baseName}.
        /// Uses LoadAssetsAsync with MergeMode.Intersection to filter by key AND language.
        /// Runtime-only: Fails in Editor (non-Play Mode).
        /// Policy: 1 language per baseName. Changing language requires UnloadStrings + LoadStringsAsync.
        /// </summary>
        /// <param name="key">Addressables key to load TextAssets</param>
        /// <param name="format">Json or Pb64</param>
        /// <param name="language">Language for this string table</param>
        /// <param name="onError">Error callback (per-asset errors, does not stop loading)</param>
        public IEnumerator LoadStringsAsync(
            string key,
            TableFormat format,
            SystemLanguage language,
            Action<string>? onError = null)
        {
            // Editor guard: Play Mode only
#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlaying)
            {
                onError?.Invoke("[TableManager] Runtime-only. Enter Play Mode.");
                yield break;
            }
#endif

            if (language == SystemLanguage.Unknown)
                language = SystemLanguage.English;

            // Load multiple assets via Addressables with key AND language intersection
            var keys = new object[] { key, language.ToString() };
            var loadHandle = Addressables.LoadAssetsAsync<TextAsset>(
                keys,
                null,
                Addressables.MergeMode.Intersection,
                releaseDependenciesOnFailure: true
            );
            yield return loadHandle;

            if (loadHandle.Status != AsyncOperationStatus.Succeeded)
            {
                onError?.Invoke($"[TableManager] Failed to load string '{key}' for {language}: {loadHandle.OperationException?.Message}");
                Addressables.Release(loadHandle);
                yield break;
            }

            var assets = loadHandle.Result;
            if (assets == null || assets.Count == 0)
            {
                onError?.Invoke($"[TableManager] No TextAssets found for string '{key}' with language {language}");
                Addressables.Release(loadHandle);
                yield break;
            }

            // Create shared handle with initial RefCount = 0
            var shared = new SharedHandle { Handle = loadHandle, RefCount = 0 };

            // Process each asset
            foreach (var textAsset in assets)
            {
                if (textAsset == null)
                {
                    onError?.Invoke($"[TableManager] Null TextAsset in string results for '{key}'");
                    continue;
                }

                var fileName = textAsset.name;
                var baseName = ExtractBaseName(fileName);

                // Check ST loader registration
                if (!mStLoaders.TryGetValue(baseName, out var stLoader))
                {
                    onError?.Invoke($"[TableManager] No ST loader registered for '{baseName}'. " +
                        $"Call RegisterStLoader(\"{baseName}\", ...) first.");
                    continue;
                }

                // Language policy: 1 language per baseName (per-asset skip, not global break)
                if (mStActiveLanguages.TryGetValue(baseName, out var existingLang))
                {
                    if (existingLang != language)
                    {
                        onError?.Invoke($"[TableManager] ST '{baseName}' already loaded with {existingLang}. " +
                            $"To change to {language}, call UnloadStrings(\"{baseName}\") first.");
                        continue;
                    }
                }

                // For strings, include language in cache key
                var cacheFileName = $"{fileName}:{language}";
                var cacheKey = new CacheKey(format, cacheFileName);

                // Already cached? Skip (do NOT increment RefCount)
                if (mCache.ContainsKey(cacheKey))
                {
                    continue;
                }

                // Parse data and insert to ST
                var cachedData = new CachedData { Shared = shared };

                try
                {
                    var text = textAsset.text;
                    cachedData.NdjsonText = text; // Store raw text for both formats
                    
                    // ST loader receives raw text (ndjson or pb64 text) and parses internally
                    if (format == TableFormat.Json)
                    {
                        stLoader(format, language, text, null);
                    }
                    else // Pb64
                    {
                        stLoader(format, language, null, text);
                    }
                }
                catch (Exception ex)
                {
                    onError?.Invoke($"[TableManager] Parse/insert error for string '{fileName}': {ex.Message}");
                    continue;
                }

                // Success: cache, track language, and increment RefCount
                mCache[cacheKey] = cachedData;
                mStActiveLanguages[baseName] = language;
                shared.RefCount++;
            }

            // If all assets were skipped, release the handle immediately
            if (shared.RefCount == 0)
            {
                Addressables.Release(loadHandle);
            }
        }

        // ====================================================================
        // Helper: Extract baseName from fileName
        // ====================================================================

        /// <summary>
        /// Extract base table name from fileName.
        /// If fileName is "{TableName}@{Description}", returns "{TableName}".
        /// Otherwise returns fileName as-is.
        /// </summary>
        public static string ExtractBaseName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return fileName;

            var atIndex = fileName.IndexOf('@');
            return atIndex >= 0 ? fileName.Substring(0, atIndex) : fileName;
        }

        // ====================================================================
        // Unload (with SharedHandle refcount)
        // ====================================================================

        /// <summary>
        /// Unload a cached table by fileName.
        /// Uses ReleaseShared for safe refcount-based release.
        /// </summary>
        public void Unload(TableFormat format, string fileName)
        {
            var cacheKey = new CacheKey(format, fileName);
            if (mCache.TryGetValue(cacheKey, out var data))
            {
                ReleaseShared(data);
                mCache.Remove(cacheKey);
            }
        }

        /// <summary>
        /// Unload a cached string table by baseName.
        /// Clears the active language tracking for this baseName.
        /// Uses ReleaseShared for safe refcount-based release.
        /// </summary>
        public void UnloadStrings(string baseName)
        {
            // Find and remove all cache entries for this baseName
            var keysToRemove = new List<CacheKey>();
            foreach (var kvp in mCache)
            {
                // Check if this is an ST cache entry for the given baseName
                var fileName = kvp.Key.FileName;
                if (fileName.Contains(':'))
                {
                    var fileBaseName = fileName.Substring(0, fileName.LastIndexOf(':'));
                    if (ExtractBaseName(fileBaseName) == baseName)
                    {
                        keysToRemove.Add(kvp.Key);
                        ReleaseShared(kvp.Value);
                    }
                }
            }

            foreach (var key in keysToRemove)
            {
                mCache.Remove(key);
            }

            // Clear language tracking
            mStActiveLanguages.Remove(baseName);
        }

        /// <summary>
        /// Unload all cached data.
        /// Uses ReleaseShared for safe refcount-based release.
        /// Note: Same SharedHandle may be referenced by multiple entries,
        /// but ReleaseShared ensures it's only released once (when RefCount reaches 0).
        /// </summary>
        public void UnloadAll()
        {
            foreach (var data in mCache.Values)
            {
                ReleaseShared(data);
            }
            mCache.Clear();
            mStActiveLanguages.Clear();
        }

        /// <summary>
        /// Check if data is cached.
        /// </summary>
        public bool IsCached(TableFormat format, string fileName)
        {
            var cacheKey = new CacheKey(format, fileName);
            return mCache.ContainsKey(cacheKey);
        }

        // ====================================================================
        // Lifecycle
        // ====================================================================

        /// <summary>
        /// OnDestroy: Clear cache and release handles.
        /// </summary>
        protected override void OnDestroy()
        {
            UnloadAll();
            mTbLoaders.Clear();
            mStLoaders.Clear();
            base.OnDestroy();
        }
    }
}
