// SSOT: skills/devian/33-string-table/SKILL.md
// Devian Unity String Table Manager
// Addressables-based string table loading with DownloadManager integration

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Devian
{
    /// <summary>
    /// String Table Manager for loading and caching multi-language text tables.
    /// Uses DownloadManager for downloading and Addressables for loading.
    /// Cache key: (format, language, tableName) - NOT compatible with AssetManager name cache.
    /// </summary>
    public class StringTableManager : ResSingleton<StringTableManager>
    {
        // ====================================================================
        // Cache Key
        // ====================================================================

        private readonly struct CacheKey : IEquatable<CacheKey>
        {
            public readonly string Format;
            public readonly SystemLanguage Language;
            public readonly string TableName;

            public CacheKey(string format, SystemLanguage language, string tableName)
            {
                Format = format;
                Language = language;
                TableName = tableName;
            }

            public bool Equals(CacheKey other)
            {
                return Format == other.Format && Language == other.Language && TableName == other.TableName;
            }

            public override bool Equals(object? obj) => obj is CacheKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Format, Language, TableName);

            public string ToAddressableKey() => $"string/{Format}/{Language}/{TableName}";
        }

        // ====================================================================
        // Cache
        // ====================================================================

        private sealed class TableData
        {
            public AsyncOperationHandle Handle;
            public readonly Dictionary<string, string> Entries = new();
        }

        private readonly Dictionary<CacheKey, TableData> mTables = new();

        // ====================================================================
        // Download + Load
        // ====================================================================

        /// <summary>
        /// Download and load a string table.
        /// Uses DownloadManager for downloading, then Addressables for loading.
        /// </summary>
        /// <param name="format">"ndjson" or "pb64"</param>
        /// <param name="language">Language (Unknown defaults to English)</param>
        /// <param name="tableName">Table name</param>
        /// <param name="onProgress">Download progress callback (0-1)</param>
        /// <param name="onError">Error callback</param>
        public IEnumerator PreloadAsync(
            string format,
            SystemLanguage language,
            string tableName,
            Action<float>? onProgress = null,
            Action<string>? onError = null)
        {
            // Default language
            if (language == SystemLanguage.Unknown)
                language = SystemLanguage.English;

            var cacheKey = new CacheKey(format, language, tableName);

            // Already loaded?
            if (mTables.ContainsKey(cacheKey))
            {
                onProgress?.Invoke(1f);
                yield break;
            }

            var addressableKey = cacheKey.ToAddressableKey();
            var labels = new[] { addressableKey };

            // Download via DownloadManager
            var dm = DownloadManager.Instance;
            if (dm != null)
            {
                // Patch
                yield return dm.PatchProc(
                    _ => { },
                    err => onError?.Invoke(err),
                    labels
                );

                // Download
                bool downloadSuccess = false;
                yield return dm.DownloadProc(
                    progress => onProgress?.Invoke(progress * 0.5f), // 0-50%
                    () => downloadSuccess = true,
                    err => onError?.Invoke(err),
                    labels
                );

                if (!downloadSuccess)
                {
                    yield break;
                }
            }

            // Load via Addressables
            var loadHandle = Addressables.LoadAssetAsync<TextAsset>(addressableKey);
            yield return loadHandle;

            if (loadHandle.Status != AsyncOperationStatus.Succeeded)
            {
                onError?.Invoke($"[StringTableManager] Failed to load '{addressableKey}': {loadHandle.OperationException?.Message}");
                Addressables.Release(loadHandle);
                yield break;
            }

            var textAsset = loadHandle.Result;
            if (textAsset == null)
            {
                onError?.Invoke($"[StringTableManager] TextAsset is null for '{addressableKey}'");
                Addressables.Release(loadHandle);
                yield break;
            }

            // Parse content
            var tableData = new TableData { Handle = loadHandle };

            try
            {
                if (format == "ndjson")
                {
                    ParseNdjson(textAsset.text, tableData.Entries);
                }
                else if (format == "pb64")
                {
                    ParsePb64(textAsset.text, tableData.Entries);
                }
                else
                {
                    onError?.Invoke($"[StringTableManager] Unknown format '{format}'");
                    Addressables.Release(loadHandle);
                    yield break;
                }
            }
            catch (Exception ex)
            {
                onError?.Invoke($"[StringTableManager] Parse error: {ex.Message}");
                Addressables.Release(loadHandle);
                yield break;
            }

            mTables[cacheKey] = tableData;
            onProgress?.Invoke(1f);
        }

        /// <summary>
        /// Load a string table without downloading (assumes already downloaded).
        /// </summary>
        public IEnumerator LoadAsync(
            string format,
            SystemLanguage language,
            string tableName,
            Action<string>? onError = null)
        {
            yield return PreloadAsync(format, language, tableName, null, onError);
        }

        // ====================================================================
        // Unload
        // ====================================================================

        /// <summary>
        /// Unload a string table and release Addressables handle.
        /// </summary>
        public void Unload(string format, SystemLanguage language, string tableName)
        {
            if (language == SystemLanguage.Unknown)
                language = SystemLanguage.English;

            var cacheKey = new CacheKey(format, language, tableName);

            if (mTables.TryGetValue(cacheKey, out var tableData))
            {
                if (tableData.Handle.IsValid())
                {
                    Addressables.Release(tableData.Handle);
                }
                mTables.Remove(cacheKey);
            }
        }

        /// <summary>
        /// Unload all cached tables.
        /// </summary>
        public void UnloadAll()
        {
            foreach (var tableData in mTables.Values)
            {
                if (tableData.Handle.IsValid())
                {
                    Addressables.Release(tableData.Handle);
                }
            }
            mTables.Clear();
        }

        // ====================================================================
        // Get
        // ====================================================================

        /// <summary>
        /// Get text by id from string table.
        /// Fallback: current language → English → id
        /// </summary>
        /// <param name="format">"ndjson" or "pb64"</param>
        /// <param name="language">Language (Unknown defaults to English)</param>
        /// <param name="tableName">Table name</param>
        /// <param name="id">Text id</param>
        /// <returns>Text value, English fallback, or id if not found</returns>
        public string Get(string format, SystemLanguage language, string tableName, string id)
        {
            if (language == SystemLanguage.Unknown)
                language = SystemLanguage.English;

            var cacheKey = new CacheKey(format, language, tableName);

            // Try current language
            if (mTables.TryGetValue(cacheKey, out var tableData))
            {
                if (tableData.Entries.TryGetValue(id, out var text) && !string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }

            // Fallback to English
            if (language != SystemLanguage.English)
            {
                var englishKey = new CacheKey(format, SystemLanguage.English, tableName);
                if (mTables.TryGetValue(englishKey, out var englishData))
                {
                    if (englishData.Entries.TryGetValue(id, out var text) && !string.IsNullOrEmpty(text))
                    {
                        return text;
                    }
                }
            }

            // Fallback to id
            return id;
        }

        /// <summary>
        /// Get text using default language (English).
        /// </summary>
        public string Get(string format, string tableName, string id)
        {
            return Get(format, SystemLanguage.English, tableName, id);
        }

        /// <summary>
        /// Check if a table is loaded.
        /// </summary>
        public bool IsLoaded(string format, SystemLanguage language, string tableName)
        {
            if (language == SystemLanguage.Unknown)
                language = SystemLanguage.English;

            var cacheKey = new CacheKey(format, language, tableName);
            return mTables.ContainsKey(cacheKey);
        }

        // ====================================================================
        // Parsing
        // ====================================================================

        /// <summary>
        /// Parse ndjson format (one JSON object per line).
        /// Format: {"id":"...","text":"..."}
        /// </summary>
        private void ParseNdjson(string content, Dictionary<string, string> entries)
        {
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                // Simple JSON parsing for {"id":"...","text":"..."}
                var entry = JsonUtility.FromJson<NdjsonEntry>(trimmed);
                if (entry != null && !string.IsNullOrEmpty(entry.id))
                {
                    entries[entry.id] = entry.text ?? string.Empty;
                }
            }
        }

        [Serializable]
        private class NdjsonEntry
        {
            public string id = string.Empty;
            public string text = string.Empty;
        }

        /// <summary>
        /// Parse pb64 format (multiple base64 chunks, each containing protobuf StringChunk).
        /// </summary>
        private void ParsePb64(string content, Dictionary<string, string> entries)
        {
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                try
                {
                    var bytes = Convert.FromBase64String(trimmed);
                    ParseStringChunk(bytes, entries);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[StringTableManager] Failed to parse pb64 line: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Parse protobuf StringChunk message.
        /// message StringChunk { repeated StringEntry entries = 1; }
        /// message StringEntry { string id = 1; string text = 2; }
        /// </summary>
        private void ParseStringChunk(byte[] bytes, Dictionary<string, string> entries)
        {
            int offset = 0;
            while (offset < bytes.Length)
            {
                // Read tag
                var tag = ReadVarint(bytes, ref offset);
                var fieldNumber = (int)(tag >> 3);
                var wireType = (int)(tag & 0x7);

                if (fieldNumber == 1 && wireType == 2) // entries (embedded message)
                {
                    var length = (int)ReadVarint(bytes, ref offset);
                    var entryBytes = new byte[length];
                    Array.Copy(bytes, offset, entryBytes, 0, length);
                    offset += length;

                    var (id, text) = ParseStringEntry(entryBytes);
                    if (!string.IsNullOrEmpty(id))
                    {
                        entries[id] = text;
                    }
                }
                else
                {
                    // Skip unknown field
                    SkipField(bytes, ref offset, wireType);
                }
            }
        }

        /// <summary>
        /// Parse protobuf StringEntry message.
        /// </summary>
        private (string id, string text) ParseStringEntry(byte[] bytes)
        {
            string id = string.Empty;
            string text = string.Empty;

            int offset = 0;
            while (offset < bytes.Length)
            {
                var tag = ReadVarint(bytes, ref offset);
                var fieldNumber = (int)(tag >> 3);
                var wireType = (int)(tag & 0x7);

                if (wireType == 2) // length-delimited (string)
                {
                    var length = (int)ReadVarint(bytes, ref offset);
                    var str = Encoding.UTF8.GetString(bytes, offset, length);
                    offset += length;

                    if (fieldNumber == 1) id = str;
                    else if (fieldNumber == 2) text = str;
                }
                else
                {
                    SkipField(bytes, ref offset, wireType);
                }
            }

            return (id, text);
        }

        /// <summary>
        /// Read a varint from bytes.
        /// </summary>
        private ulong ReadVarint(byte[] bytes, ref int offset)
        {
            ulong result = 0;
            int shift = 0;
            while (offset < bytes.Length)
            {
                var b = bytes[offset++];
                result |= (ulong)(b & 0x7f) << shift;
                if ((b & 0x80) == 0) break;
                shift += 7;
            }
            return result;
        }

        /// <summary>
        /// Skip a protobuf field based on wire type.
        /// </summary>
        private void SkipField(byte[] bytes, ref int offset, int wireType)
        {
            switch (wireType)
            {
                case 0: // Varint
                    ReadVarint(bytes, ref offset);
                    break;
                case 1: // 64-bit
                    offset += 8;
                    break;
                case 2: // Length-delimited
                    var length = (int)ReadVarint(bytes, ref offset);
                    offset += length;
                    break;
                case 5: // 32-bit
                    offset += 4;
                    break;
            }
        }
    }
}
