// SSOT: skills/devian-unity/10-base-system/12-download-manager/SKILL.md
// Devian Unity Download Manager - Addressables Label based Patch/Download
// CompoSingleton: Bootstrap에서 생성/등록되거나 씬에 배치해야 함

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Devian
{
    /// <summary>
    /// Patch information containing total download size and per-label breakdown.
    /// </summary>
    public sealed class PatchInfo
    {
        /// <summary>
        /// Total download size in bytes across all labels.
        /// </summary>
        public long TotalSize { get; }

        /// <summary>
        /// Download size per label in bytes.
        /// </summary>
        public IReadOnlyDictionary<string, long> LabelSizes { get; }

        public PatchInfo(long totalSize, Dictionary<string, long> labelSizes)
        {
            TotalSize = totalSize;
            LabelSizes = labelSizes;
        }
    }

    /// <summary>
    /// Addressables Label-based Patch/Download manager.
    /// Inspector에서 patchLabels를 설정하고, PatchProc/DownloadProc로 다운로드 수행.
    ///
    /// CompoSingleton-based: Bootstrap에서 생성/등록되거나 씬에 배치해야 함.
    /// </summary>
    public sealed class DownloadManager : CompoSingleton<DownloadManager>
    {
        // ====================================================================
        // Inspector Fields
        // ====================================================================
        
        [SerializeField]
        [Tooltip("Addressables Labels to patch/download")]
        private List<string> patchLabels = new List<string>();
        
        [SerializeField]
        [Tooltip("Clear dependency cache before calculating size (DANGER: use only for testing)")]
        private bool forceClearDependencyCache = false;
        
        // ====================================================================
        // Events
        // ====================================================================
        
        /// <summary>
        /// Fired when an error occurs during patch/download.
        /// Note: onError callback is always called; this is an additional notification channel.
        /// </summary>
        public event Action<string>? OnError;
        
        // ====================================================================
        // Public Properties
        // ====================================================================
        
        /// <summary>
        /// Read-only access to configured patch labels.
        /// </summary>
        public IReadOnlyList<string> PatchLabels => patchLabels;
        
        /// <summary>
        /// Cached PatchInfo from last PatchProc call.
        /// </summary>
        public PatchInfo? LastPatchInfo { get; private set; }
        
        // ====================================================================
        // Label Normalization
        // ====================================================================
        
        /// <summary>
        /// Normalizes labels: trim, remove empty, distinct, sort (Ordinal).
        /// </summary>
        private static List<string> NormalizeLabels(IReadOnlyList<string>? labels)
        {
            if (labels == null || labels.Count == 0)
                return new List<string>();
            
            return labels
                .Select(l => l?.Trim() ?? string.Empty)
                .Where(l => !string.IsNullOrEmpty(l))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(l => l, StringComparer.Ordinal)
                .ToList();
        }
        
        // ====================================================================
        // PatchProc - Calculate download sizes
        // ====================================================================
        
        /// <summary>
        /// Calculates download size for each label.
        /// </summary>
        /// <param name="onDone">Called with PatchInfo on success</param>
        /// <param name="onError">Called with error message on failure (never silent)</param>
        /// <param name="overrideLabels">Optional: override patchLabels for this call</param>
        public IEnumerator PatchProc(
            Action<PatchInfo> onDone,
            Action<string>? onError = null,
            IReadOnlyList<string>? overrideLabels = null)
        {
            var labels = NormalizeLabels(overrideLabels ?? patchLabels);
            
            // Empty labels = 0 bytes, immediate success
            if (labels.Count == 0)
            {
                var emptyInfo = new PatchInfo(0, new Dictionary<string, long>());
                LastPatchInfo = emptyInfo;
                onDone?.Invoke(emptyInfo);
                yield break;
            }
            
            var labelSizes = new Dictionary<string, long>();
            long totalSize = 0;
            
            foreach (var label in labels)
            {
                // Optional: Clear dependency cache (DANGEROUS - only for testing)
                if (forceClearDependencyCache)
                {
                    var clearOp = Addressables.ClearDependencyCacheAsync(label, false);
                    yield return clearOp;
                    
                    if (clearOp.Status == AsyncOperationStatus.Failed)
                    {
                        var msg = $"[DownloadManager] ClearDependencyCacheAsync failed for label '{label}': {clearOp.OperationException?.Message}";
                        Debug.LogError(msg);
                        RaiseError(msg, onError);
                        yield break;
                    }
                    
                    Addressables.Release(clearOp);
                }
                
                // Get download size
                var sizeOp = Addressables.GetDownloadSizeAsync(label);
                yield return sizeOp;
                
                if (sizeOp.Status == AsyncOperationStatus.Failed)
                {
                    var msg = $"[DownloadManager] GetDownloadSizeAsync failed for label '{label}': {sizeOp.OperationException?.Message}";
                    Debug.LogError(msg);
                    RaiseError(msg, onError);
                    yield break;
                }
                
                var size = sizeOp.Result;
                labelSizes[label] = size;
                totalSize += size;
                
                Addressables.Release(sizeOp);
            }
            
            var patchInfo = new PatchInfo(totalSize, labelSizes);
            LastPatchInfo = patchInfo;
            onDone?.Invoke(patchInfo);
        }
        
        // ====================================================================
        // DownloadProc - Download dependencies
        // ====================================================================
        
        /// <summary>
        /// Downloads dependencies for each label.
        /// </summary>
        /// <param name="onProgress">Called with progress 0~1</param>
        /// <param name="onSuccess">Called on successful completion</param>
        /// <param name="onError">Called with error message on failure (never silent)</param>
        /// <param name="overrideLabels">Optional: override patchLabels for this call</param>
        public IEnumerator DownloadProc(
            Action<float>? onProgress,
            Action onSuccess,
            Action<string>? onError = null,
            IReadOnlyList<string>? overrideLabels = null)
        {
            var labels = NormalizeLabels(overrideLabels ?? patchLabels);
            
            // Empty labels = immediate success
            if (labels.Count == 0)
            {
                onProgress?.Invoke(1f);
                onSuccess?.Invoke();
                yield break;
            }
            
            // Need PatchInfo for weighted progress
            PatchInfo? patchInfo = LastPatchInfo;
            
            // If no cached PatchInfo or labels differ, run PatchProc first
            if (patchInfo == null || !LabelsMatch(labels, patchInfo.LabelSizes.Keys))
            {
                PatchInfo? fetchedInfo = null;
                string? patchError = null;
                
                yield return PatchProc(
                    info => fetchedInfo = info,
                    err => patchError = err,
                    labels
                );
                
                if (patchError != null)
                {
                    // PatchProc already raised error
                    yield break;
                }
                
                patchInfo = fetchedInfo!;
            }
            
            // Nothing to download
            if (patchInfo.TotalSize == 0)
            {
                onProgress?.Invoke(1f);
                onSuccess?.Invoke();
                yield break;
            }
            
            long downloadedBytes = 0;
            long totalBytes = patchInfo.TotalSize;
            
            foreach (var label in labels)
            {
                if (!patchInfo.LabelSizes.TryGetValue(label, out var labelSize) || labelSize == 0)
                {
                    // Nothing to download for this label
                    continue;
                }
                
                var downloadOp = Addressables.DownloadDependenciesAsync(label, false);
                
                while (!downloadOp.IsDone)
                {
                    // Report weighted progress
                    var labelProgress = downloadOp.PercentComplete;
                    var currentLabelBytes = (long)(labelSize * labelProgress);
                    var totalProgress = (downloadedBytes + currentLabelBytes) / (float)totalBytes;
                    onProgress?.Invoke(Mathf.Clamp01(totalProgress));
                    yield return null;
                }
                
                if (downloadOp.Status == AsyncOperationStatus.Failed)
                {
                    var msg = $"[DownloadManager] DownloadDependenciesAsync failed for label '{label}': {downloadOp.OperationException?.Message}";
                    Debug.LogError(msg);
                    Addressables.Release(downloadOp);
                    RaiseError(msg, onError);
                    // IMPORTANT: Do NOT call onSuccess after error
                    yield break;
                }
                
                downloadedBytes += labelSize;
                Addressables.Release(downloadOp);
            }
            
            onProgress?.Invoke(1f);
            onSuccess?.Invoke();
        }
        
        // ====================================================================
        // Helper Methods
        // ====================================================================
        
        private void RaiseError(string message, Action<string>? callback)
        {
            // Always invoke callback (never silent)
            callback?.Invoke(message);
            // Also raise event for additional listeners
            OnError?.Invoke(message);
        }
        
        private static bool LabelsMatch(IReadOnlyList<string> a, IEnumerable<string> b)
        {
            var setA = new HashSet<string>(a);
            var setB = new HashSet<string>(b);
            return setA.SetEquals(setB);
        }
    }
}
