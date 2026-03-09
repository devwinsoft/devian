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


    public async Task LoadBundlesAsync(SystemLanguage language)
    {
#if UNITY_EDITOR
        await TableManager.Instance.LoadTablesAsync("table-ndjson", TableFormat.Json);
        await TableManager.Instance.LoadStringsAsync("string-ndjson", TableFormat.Json, language);
#else
        await TableManager.Instance.LoadTablesAsync("table-pb64", TableFormat.Pb64);
        await TableManager.Instance.LoadStringsAsync("string-pb64", TableFormat.Pb64, language);
#endif

        await AssetManager.LoadBundleAssets<GameObject>("common-effects");
        await AssetManager.LoadBundleAssets<GameObject>("prefabs");
        await SoundManager.Instance.LoadByBundleKeyAsync("sounds");

        //await VoiceManager.Instance.LoadByBundleKeyAsync("", language, SystemLanguage.English);
    }
}
