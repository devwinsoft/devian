// SSOT: skills/devian-unity/20-domain-common-system/19-bundle-manager/SKILL.md
// Devian Unity Bundle Manager - Addressables Label based Patch/Download
// CompoSingleton: Bootstrap prefab에 배치하여 사용

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Devian.Domain.Common;

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
    /// InitializeAsync/DownloadAsync에 labels를 전달하여 다운로드 수행.
    ///
    /// CompoSingleton: Bootstrap prefab에 배치하여 사용.
    /// </summary>
    public abstract class BundleManager<T> : CompoSingleton<T> where T : BundleManager<T>
    {
        // ====================================================================
        // Inspector Fields
        // ====================================================================

        [SerializeField]
        [Tooltip("Clear dependency cache before calculating size (DANGER: use only for testing)")]
        private bool forceClearDependencyCache = false;
        
        // ====================================================================
        // Events
        // ====================================================================
        
        /// <summary>
        /// Fired when an error occurs during patch/download.
        /// Additional notification channel (errors are also returned via CommonResult).
        /// </summary>
        public event Action<string>? OnError;
        
        // ====================================================================
        // Public Properties
        // ====================================================================

        /// <summary>
        /// Cached PatchInfo from last InitializeAsync call.
        /// </summary>
        public PatchInfo? LastPatchInfo { get; private set; }

        // ====================================================================
        // InitializeAsync - Calculate download sizes
        // ====================================================================

        /// <summary>
        /// Calculates download size for each label.
        /// </summary>
        /// <param name="labels">Labels to check download size</param>
        public async Task<CommonResult<PatchInfo>> InitializeAsync(IReadOnlyList<string> labels)
        {
            // Empty labels = 0 bytes, immediate success
            if (labels.Count == 0)
            {
                var emptyInfo = new PatchInfo(0, new Dictionary<string, long>());
                LastPatchInfo = emptyInfo;
                return CommonResult<PatchInfo>.Success(emptyInfo);
            }

            var labelSizes = new Dictionary<string, long>();
            long totalSize = 0;

            foreach (var label in labels)
            {
                // Optional: Clear dependency cache (DANGEROUS - only for testing)
                if (forceClearDependencyCache)
                {
                    var clearOp = Addressables.ClearDependencyCacheAsync(label, false);
                    await clearOp.Task;

                    if (clearOp.Status == AsyncOperationStatus.Failed)
                    {
                        var msg = $"[BundleManager] ClearDependencyCacheAsync failed for label '{label}': {clearOp.OperationException?.Message}";
                        Debug.LogError(msg);
                        RaiseError(msg);
                        return CommonResult<PatchInfo>.Failure(COMMON_ERROR_TYPE.COMMON_UNKNOWN, msg);
                    }

                    Addressables.Release(clearOp);
                }

                // Get download size
                var sizeOp = Addressables.GetDownloadSizeAsync(label);
                await sizeOp.Task;

                if (sizeOp.Status == AsyncOperationStatus.Failed)
                {
                    var msg = $"[BundleManager] GetDownloadSizeAsync failed for label '{label}': {sizeOp.OperationException?.Message}";
                    Debug.LogError(msg);
                    RaiseError(msg);
                    return CommonResult<PatchInfo>.Failure(COMMON_ERROR_TYPE.COMMON_UNKNOWN, msg);
                }

                var size = sizeOp.Result;
                labelSizes[label] = size;
                totalSize += size;

                Addressables.Release(sizeOp);
            }

            var patchInfo = new PatchInfo(totalSize, labelSizes);
            LastPatchInfo = patchInfo;
            return CommonResult<PatchInfo>.Success(patchInfo);
        }
        
        // ====================================================================
        // DownloadAsync - Download dependencies
        // ====================================================================
        
        /// <summary>
        /// Downloads dependencies for each label.
        /// </summary>
        /// <param name="labels">Labels to download</param>
        /// <param name="onProgress">Called with progress 0~1</param>
        public async Task<CommonResult> DownloadAsync(
            IReadOnlyList<string> labels,
            Action<float>? onProgress = null)
        {
            // Empty labels = immediate success
            if (labels.Count == 0)
            {
                onProgress?.Invoke(1f);
                return CommonResult.Ok();
            }

            // Need PatchInfo for weighted progress
            PatchInfo? patchInfo = LastPatchInfo;

            // If no cached PatchInfo or labels differ, run InitializeAsync first
            if (patchInfo == null || !LabelsMatch(labels, patchInfo.LabelSizes.Keys))
            {
                var patchResult = await InitializeAsync(labels);
                if (patchResult.IsFailure)
                    return CommonResult.Failure(patchResult.Error!);

                patchInfo = patchResult.Value!;
            }

            // Nothing to download
            if (patchInfo.TotalSize == 0)
            {
                onProgress?.Invoke(1f);
                return CommonResult.Ok();
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
                    await Task.Yield();
                }

                if (downloadOp.Status == AsyncOperationStatus.Failed)
                {
                    var msg = $"[BundleManager] DownloadDependenciesAsync failed for label '{label}': {downloadOp.OperationException?.Message}";
                    Debug.LogError(msg);
                    Addressables.Release(downloadOp);
                    RaiseError(msg);
                    return CommonResult.Failure(COMMON_ERROR_TYPE.COMMON_UNKNOWN, msg);
                }

                downloadedBytes += labelSize;
                Addressables.Release(downloadOp);
            }

            onProgress?.Invoke(1f);
            return CommonResult.Ok();
        }
        
        // ====================================================================
        // Helper Methods
        // ====================================================================
        
        private void RaiseError(string message)
        {
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
