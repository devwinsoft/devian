// SSOT: skills/devian-unity/30-unity-components/14-table-manager/SKILL.md
// Devian Unity TableManager - Unified table loading (TB_/ST_)
// Supports ndjson and pb64 formats for both general tables and string tables

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
    /// Unified Table Manager for loading and caching tables.
    /// - General Tables: TB_{TableName} (cache key: format, tableName)
    /// - String Tables: ST_{TableName} (cache key: format, language, tableName)
    /// 
    /// Addressable Key Convention:
    /// - General: table/{format}/{TableName}
    /// - String: string/{format}/{Language}/{TableName}
    /// 
    /// Label = Key (same as key for DownloadManager compatibility)
    /// </summary>
    public class TableManager : ResSingleton<TableManager>
    {
        // ====================================================================
        // Cache Keys
        // ====================================================================

        private readonly struct TableCacheKey : IEquatable<TableCacheKey>
        {
            public readonly string Format;
            public readonly string TableName;

            public TableCacheKey(string format, string tableName)
            {
                Format = format;
                TableName = tableName;
            }

            public bool Equals(TableCacheKey other)
            {
                return Format == other.Format && TableName == other.TableName;
            }

            public override bool Equals(object? obj) => obj is TableCacheKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Format, TableName);

            public string ToAddressableKey() => $"table/{Format}/{TableName}";
        }

        private readonly struct StringCacheKey : IEquatable<StringCacheKey>
        {
            public readonly string Format;
            public readonly SystemLanguage Language;
            public readonly string TableName;

            public StringCacheKey(string format, SystemLanguage language, string tableName)
            {
                Format = format;
                Language = language;
                TableName = tableName;
            }

            public bool Equals(StringCacheKey other)
            {
                return Format == other.Format && Language == other.Language && TableName == other.TableName;
            }

            public override bool Equals(object? obj) => obj is StringCacheKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Format, Language, TableName);

            public string ToAddressableKey() => $"string/{Format}/{Language}/{TableName}";
        }

        // ====================================================================
        // Cache Data
        // ====================================================================

        private sealed class TableData
        {
            public AsyncOperationHandle Handle;
            public string? RawText;      // For ndjson
            public byte[]? RawBinary;    // For pb64
        }

        private sealed class StringData
        {
            public AsyncOperationHandle Handle;
            public readonly Dictionary<string, string> Entries = new();
        }

        private readonly Dictionary<TableCacheKey, TableData> mTables = new();
        private readonly Dictionary<StringCacheKey, StringData> mStrings = new();

        // ====================================================================
        // General Table Loading
        // ====================================================================

        /// <summary>
        /// Preload a general table (TB_{TableName}).
        /// </summary>
        /// <param name="format">"ndjson" or "pb64"</param>
        /// <param name="tableName">Table name (e.g., "TestSheet")</param>
        /// <param name="onLoaded">Callback with (rawText for ndjson, rawBinary for pb64)</param>
        /// <param name="onProgress">Download progress callback (0-1)</param>
        /// <param name="onError">Error callback</param>
        public IEnumerator PreloadTableAsync(
            string format,
            string tableName,
            Action<string?, byte[]?>? onLoaded = null,
            Action<float>? onProgress = null,
            Action<string>? onError = null)
        {
            var cacheKey = new TableCacheKey(format, tableName);

            // Already loaded?
            if (mTables.TryGetValue(cacheKey, out var existing))
            {
                onProgress?.Invoke(1f);
                onLoaded?.Invoke(existing.RawText, existing.RawBinary);
                yield break;
            }

            var addressableKey = cacheKey.ToAddressableKey();
            var labels = new[] { addressableKey };

            // Download via DownloadManager
            var dm = DownloadManager.Instance;
            if (dm != null)
            {
                yield return dm.PatchProc(
                    _ => { },
                    err => onError?.Invoke(err),
                    labels
                );

                bool downloadSuccess = false;
                yield return dm.DownloadProc(
                    progress => onProgress?.Invoke(progress * 0.5f),
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
                onError?.Invoke($"[TableManager] Failed to load '{addressableKey}': {loadHandle.OperationException?.Message}");
                Addressables.Release(loadHandle);
                yield break;
            }

            var textAsset = loadHandle.Result;
            if (textAsset == null)
            {
                onError?.Invoke($"[TableManager] TextAsset is null for '{addressableKey}'");
                Addressables.Release(loadHandle);
                yield break;
            }

            var tableData = new TableData { Handle = loadHandle };

            try
            {
                if (format == "ndjson")
                {
                    tableData.RawText = textAsset.text;
                }
                else if (format == "pb64")
                {
                    // DVGB container → raw binary
                    tableData.RawBinary = Pb64Loader.LoadFromBase64(textAsset.text);
                }
                else
                {
                    onError?.Invoke($"[TableManager] Unknown format '{format}'");
                    Addressables.Release(loadHandle);
                    yield break;
                }
            }
            catch (Exception ex)
            {
                onError?.Invoke($"[TableManager] Parse error: {ex.Message}");
                Addressables.Release(loadHandle);
                yield break;
            }

            mTables[cacheKey] = tableData;
            onProgress?.Invoke(1f);
            onLoaded?.Invoke(tableData.RawText, tableData.RawBinary);
        }

        /// <summary>
        /// Check if a general table is loaded.
        /// </summary>
        public bool IsTableLoaded(string format, string tableName)
        {
            var cacheKey = new TableCacheKey(format, tableName);
            return mTables.ContainsKey(cacheKey);
        }

        /// <summary>
        /// Unload a general table and release Addressables handle.
        /// </summary>
        public void UnloadTable(string format, string tableName)
        {
            var cacheKey = new TableCacheKey(format, tableName);

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
        /// Get raw text for ndjson format table.
        /// </summary>
        public string? GetTableText(string tableName)
        {
            var cacheKey = new TableCacheKey("ndjson", tableName);
            return mTables.TryGetValue(cacheKey, out var data) ? data.RawText : null;
        }

        /// <summary>
        /// Get raw binary for pb64 format table.
        /// </summary>
        public byte[]? GetTableBinary(string tableName)
        {
            var cacheKey = new TableCacheKey("pb64", tableName);
            return mTables.TryGetValue(cacheKey, out var data) ? data.RawBinary : null;
        }

        // ====================================================================
        // String Table Loading
        // ====================================================================

        /// <summary>
        /// Preload a string table (ST_{TableName}).
        /// </summary>
        /// <param name="format">"ndjson" or "pb64"</param>
        /// <param name="language">Language (Unknown defaults to English)</param>
        /// <param name="tableName">Table name</param>
        /// <param name="onProgress">Download progress callback (0-1)</param>
        /// <param name="onError">Error callback</param>
        public IEnumerator PreloadStringAsync(
            string format,
            SystemLanguage language,
            string tableName,
            Action<float>? onProgress = null,
            Action<string>? onError = null)
        {
            if (language == SystemLanguage.Unknown)
                language = SystemLanguage.English;

            var cacheKey = new StringCacheKey(format, language, tableName);

            // Already loaded?
            if (mStrings.ContainsKey(cacheKey))
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
                yield return dm.PatchProc(
                    _ => { },
                    err => onError?.Invoke(err),
                    labels
                );

                bool downloadSuccess = false;
                yield return dm.DownloadProc(
                    progress => onProgress?.Invoke(progress * 0.5f),
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
                onError?.Invoke($"[TableManager] Failed to load string '{addressableKey}': {loadHandle.OperationException?.Message}");
                Addressables.Release(loadHandle);
                yield break;
            }

            var textAsset = loadHandle.Result;
            if (textAsset == null)
            {
                onError?.Invoke($"[TableManager] TextAsset is null for string '{addressableKey}'");
                Addressables.Release(loadHandle);
                yield break;
            }

            var stringData = new StringData { Handle = loadHandle };

            try
            {
                if (format == "ndjson")
                {
                    ParseStringNdjson(textAsset.text, stringData.Entries);
                }
                else if (format == "pb64")
                {
                    ParseStringPb64(textAsset.text, stringData.Entries);
                }
                else
                {
                    onError?.Invoke($"[TableManager] Unknown format '{format}'");
                    Addressables.Release(loadHandle);
                    yield break;
                }
            }
            catch (Exception ex)
            {
                onError?.Invoke($"[TableManager] String parse error: {ex.Message}");
                Addressables.Release(loadHandle);
                yield break;
            }

            mStrings[cacheKey] = stringData;
            onProgress?.Invoke(1f);
        }

        /// <summary>
        /// Check if a string table is loaded.
        /// </summary>
        public bool IsStringLoaded(string format, SystemLanguage language, string tableName)
        {
            if (language == SystemLanguage.Unknown)
                language = SystemLanguage.English;

            var cacheKey = new StringCacheKey(format, language, tableName);
            return mStrings.ContainsKey(cacheKey);
        }

        /// <summary>
        /// Unload a string table and release Addressables handle.
        /// </summary>
        public void UnloadString(string format, SystemLanguage language, string tableName)
        {
            if (language == SystemLanguage.Unknown)
                language = SystemLanguage.English;

            var cacheKey = new StringCacheKey(format, language, tableName);

            if (mStrings.TryGetValue(cacheKey, out var stringData))
            {
                if (stringData.Handle.IsValid())
                {
                    Addressables.Release(stringData.Handle);
                }
                mStrings.Remove(cacheKey);
            }
        }

        /// <summary>
        /// Get string by id from string table.
        /// Fallback: language → English → id
        /// </summary>
        public string GetString(string format, SystemLanguage language, string tableName, string id)
        {
            if (language == SystemLanguage.Unknown)
                language = SystemLanguage.English;

            var cacheKey = new StringCacheKey(format, language, tableName);

            // Try current language
            if (mStrings.TryGetValue(cacheKey, out var stringData))
            {
                if (stringData.Entries.TryGetValue(id, out var text) && !string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }

            // Fallback to English
            if (language != SystemLanguage.English)
            {
                var englishKey = new StringCacheKey(format, SystemLanguage.English, tableName);
                if (mStrings.TryGetValue(englishKey, out var englishData))
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

        // ====================================================================
        // Unload All
        // ====================================================================

        /// <summary>
        /// Unload all cached tables and strings.
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

            foreach (var stringData in mStrings.Values)
            {
                if (stringData.Handle.IsValid())
                {
                    Addressables.Release(stringData.Handle);
                }
            }
            mStrings.Clear();
        }

        // ====================================================================
        // String Parsing (ndjson)
        // ====================================================================

        /// <summary>
        /// Parse ndjson format (one JSON object per line).
        /// Format: {"id":"...","text":"..."}
        /// </summary>
        private void ParseStringNdjson(string content, Dictionary<string, string> entries)
        {
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                var entry = JsonUtility.FromJson<StringNdjsonEntry>(trimmed);
                if (entry != null && !string.IsNullOrEmpty(entry.id))
                {
                    entries[entry.id] = entry.text ?? string.Empty;
                }
            }
        }

        [Serializable]
        private class StringNdjsonEntry
        {
            public string id = string.Empty;
            public string text = string.Empty;
        }

        // ====================================================================
        // String Parsing (pb64 - StringChunk)
        // ====================================================================

        /// <summary>
        /// Parse pb64 format (multiple base64 chunks, each containing protobuf StringChunk).
        /// </summary>
        private void ParseStringPb64(string content, Dictionary<string, string> entries)
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
                    Debug.LogWarning($"[TableManager] Failed to parse pb64 line: {ex.Message}");
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
