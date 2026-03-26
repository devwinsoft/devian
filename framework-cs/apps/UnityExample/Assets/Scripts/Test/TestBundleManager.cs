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

    public override async Task LoadBundlesAsync(SystemLanguage language, float startProgress, Action<float>? onProgress = null)
    {
        // startProgress~1.0 범위를 5단계로 균등 분배
        float range = 1f - startProgress;
        float step = range / 5f;

        onProgress?.Invoke(startProgress);

        await base.LoadBundlesAsync(language, startProgress, onProgress);

#if UNITY_EDITOR
        await TableManager.Instance.LoadTablesAsync("table-ndjson", TableFormat.Json);
        await Task.Yield();
        onProgress?.Invoke(startProgress + step * 1f);
        await TableManager.Instance.LoadStringsAsync("string-ndjson", TableFormat.Json, language);
        await Task.Yield();
        onProgress?.Invoke(startProgress + step * 2f);
#else
        await TableManager.Instance.LoadTablesAsync("table-pb64", TableFormat.Pb64);
        await Task.Yield();
        onProgress?.Invoke(startProgress + step * 1f);
        await TableManager.Instance.LoadStringsAsync("string-pb64", TableFormat.Pb64, language);
        await Task.Yield();
        onProgress?.Invoke(startProgress + step * 2f);
#endif

        await AssetManager.LoadBundleAssets<GameObject>("common-effects");
        await Task.Yield();
        onProgress?.Invoke(startProgress + step * 3f);
        await AssetManager.LoadBundleAssets<GameObject>("prefabs");
        await Task.Yield();
        onProgress?.Invoke(startProgress + step * 4f);
        await SoundManager.Instance.LoadByBundleKeyAsync("sounds");
        await Task.Yield();
        onProgress?.Invoke(1f);
    }
}
