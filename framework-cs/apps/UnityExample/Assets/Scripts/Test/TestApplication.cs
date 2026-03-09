using UnityEngine;
using System;
using System.Threading.Tasks;
using Devian;

public class TestApplication : MobileApplication
{
    public static TestApplication Instance => _instance;
    static TestApplication _instance = null;

    public static TestApplication Create()
    {
        if (_instance == null)
        {
            _instance = Singleton.CreateFromResources<BaseApplication, TestApplication>("Devian/Application");
        }
        return _instance;
    }
    

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

    protected override Task onLoadCompletedAsync()
    {
        return Task.CompletedTask;
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
