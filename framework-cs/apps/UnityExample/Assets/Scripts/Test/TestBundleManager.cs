using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Devian;

public class TestBundleManager : MobileBundleManager<TestBundleManager>
{
    protected override IReadOnlyList<string> PatchLabels => new string[]
    {
        "common-effects",
        "prefabs",
        "scenes",
        "sounds",
        "ui",
#if UNITY_EDITOR
        "string-ndjson",
        "table-ndjson"
#else
        "string-pb64",
        "table-pb64"
#endif
    };

    protected override async Task onLoadBundlesAsync(SystemLanguage language, Action<float>? onProgress = null)
    {
        reportProgress(onProgress, 0f);

        await base.onLoadBundlesAsync(language, onProgress);

#if UNITY_EDITOR
        await TableManager.Instance.LoadTablesAsync("table-ndjson", TableFormat.Json);
        await Task.Yield();
        reportProgress(onProgress, 0.1f);
        await TableManager.Instance.LoadStringsAsync("string-ndjson", TableFormat.Json, language);
        await Task.Yield();
        reportProgress(onProgress, 0.2f);
#else
        await TableManager.Instance.LoadTablesAsync("table-pb64", TableFormat.Pb64);
        await Task.Yield();
        reportProgress(onProgress, 0.2f);
        await TableManager.Instance.LoadStringsAsync("string-pb64", TableFormat.Pb64, language);
        await Task.Yield();
        reportProgress(onProgress, 0.4f);
#endif

        await AssetManager.LoadBundleAssets<GameObject>("common-effects");
        await Task.Yield();
        reportProgress(onProgress, 0.4f);
        await AssetManager.LoadBundleAssets<GameObject>("prefabs");
        await Task.Yield();
        reportProgress(onProgress, 0.6f);
        await SoundManager.Instance.LoadByBundleKeyAsync("sounds");
        await Task.Yield();
        reportProgress(onProgress, 1f);
    }

    static void reportProgress(Action<float>? onProgress, float progress)
    {
        onProgress?.Invoke(Mathf.Clamp01(progress));
    }
}
