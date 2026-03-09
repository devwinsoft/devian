using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Devian.Domain.Common;
using Devian;

public class TestBundleManager : BundleManager<TestBundleManager>
{
    string[] _patchList = new string[]
    { "common-effects"
        , "prefabs"
        , "scenes"
        , "sounds"
#if UNITY_EDITOR
        , "string-ndjson"
        , "table-ndjson"
#else
    , "string-pb64"
    , "table-pb64"
#endif
    };
    bool _initialized = false;
    
    public async Task<CommonResult<PatchInfo>> InitializeAsync()
    {
        return await base.InitializeAsync(_patchList);
    }


    public async Task<CommonResult> DownloadAsync(Action<float>? onProgress = null)
    {
        var downloadResult = await DownloadAsync(
            _patchList,
            onProgress: onProgress
        );
        return downloadResult;
    }


    public async Task LoadBundlesAsync(SystemLanguage language, Action<float>? onProgress = null)
    {
        reportProgress(onProgress, 0f);

#if UNITY_EDITOR
        await TableManager.Instance.LoadTablesAsync("table-ndjson", TableFormat.Json);
        await Task.Yield();
        reportProgress(onProgress, 0.2f);
        await TableManager.Instance.LoadStringsAsync("string-ndjson", TableFormat.Json, language);
        await Task.Yield();
        reportProgress(onProgress, 0.4f);
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
        reportProgress(onProgress, 0.6f);
        await AssetManager.LoadBundleAssets<GameObject>("prefabs");
        await Task.Yield();
        reportProgress(onProgress, 0.8f);
        await SoundManager.Instance.LoadByBundleKeyAsync("sounds");
        await Task.Yield();
        reportProgress(onProgress, 1f);

        //await VoiceManager.Instance.LoadByBundleKeyAsync("", language, SystemLanguage.English);
    }

    static void reportProgress(Action<float>? onProgress, float progress)
    {
        onProgress?.Invoke(Mathf.Clamp01(progress));
    }
}
