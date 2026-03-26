using UnityEngine;
using System;
using System.Threading.Tasks;
using Devian;

public class TestApplication : MobileApplication
{
    public new static TestApplication Instance => BaseApplication.Instance as TestApplication;
    public new static TestApplication Create() => BaseApplication.Create<TestApplication>();


    protected override async Task onBootAsync()
    {
        await base.onBootAsync();
    }

    protected override async Task onLoadAsync(Action<float>? onProgress = null)
    {
        var loadingCanvas = UILoadingCanvas.Instance;
        loadingCanvas?.ShowBundleLoading();

        try
        {
            reportProgress(onProgress, 0f);

            var patchResult = await TestBundleManager.Instance.InitializeAsync();
            await Task.Yield();
            if (patchResult.IsSuccess)
            {
                Debug.Log(patchResult.Value!.TotalSize);
            }

            if (patchResult.IsSuccess && patchResult.Value != null && patchResult.Value.TotalSize > 0)
            {
                var downloadResult = await TestBundleManager.Instance.DownloadAsync(
                    0.5f,
                    progress => reportProgress(onProgress, progress));
                await Task.Yield();
                if (downloadResult.IsFailure)
                {
                    Debug.LogWarning($"[TestApplication] Bundle download failed (non-fatal): {downloadResult.Error}");
                }
            }
            else
            {
                reportProgress(onProgress, 0.5f);
                await Task.Yield();
            }

            await TestBundleManager.Instance.LoadBundlesAsync(
                DefaultLanguage,
                0.5f,
                progress => reportProgress(onProgress, progress));
            await Task.Yield();
            reportProgress(onProgress, 1f);
        }
        finally
        {
            loadingCanvas?.HideBundleLoading();
        }
    }

    protected override async Task onLoadCompletedAsync()
    {
        await base.onLoadCompletedAsync();
    }

    static void reportProgress(Action<float>? onProgress, float progress)
    {
        var clamped = Mathf.Clamp01(progress);
        onProgress?.Invoke(clamped);

        var loadingCanvas = UILoadingCanvas.Instance;
        if (loadingCanvas != null)
        {
            loadingCanvas.SetBundleLoadingProgress(clamped);
        }
    }
}
