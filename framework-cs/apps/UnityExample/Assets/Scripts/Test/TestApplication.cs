using UnityEngine;
using System;
using System.Threading.Tasks;
using Devian;

public class TestApplication : MobileApplication
{
    public new static TestApplication Create() => BaseApplication.Create<TestApplication>();


    protected override async Task onBootAsync()
    {
        await base.onBootAsync();
    }

    protected override async Task onLoadAsync(SystemLanguage language, Action<float>? onProgress = null)
    {
        reportProgress(onProgress, 0f);

        var patchResult = await TestBundleManager.Instance.InitializeAsync();
        await Task.Yield();
        reportProgress(onProgress, 0.15f);
        if (patchResult.IsSuccess)
        {
            Debug.Log(patchResult.Value!.TotalSize);
        }

        if (patchResult.IsSuccess && patchResult.Value != null && patchResult.Value.TotalSize > 0)
        {
            var downloadResult = await TestBundleManager.Instance.DownloadAsync(
                progress => reportProgress(onProgress, remapProgress(progress, 0.15f, 0.55f)));
            await Task.Yield();
            if (downloadResult.IsFailure)
            {
                Debug.LogWarning($"[TestApplication] Bundle download failed (non-fatal): {downloadResult.Error}");
            }
        }
        else
        {
            reportProgress(onProgress, 0.55f);
            await Task.Yield();
        }

        await TestBundleManager.Instance.LoadBundlesAsync(
            language,
            progress => reportProgress(onProgress, remapProgress(progress, 0.55f, 1f)));
        await Task.Yield();
        reportProgress(onProgress, 1f);
    }

    protected override async Task onLoadCompletedAsync()
    {
        await base.onLoadCompletedAsync();
    }

    static void reportProgress(Action<float>? onProgress, float progress)
    {
        onProgress?.Invoke(Mathf.Clamp01(progress));
    }

    static float remapProgress(float progress, float min, float max)
    {
        return Mathf.Lerp(min, max, Mathf.Clamp01(progress));
    }
}
