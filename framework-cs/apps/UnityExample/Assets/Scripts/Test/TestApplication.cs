using UnityEngine;
using Devian;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public class TestApplication : MobileApplication
{
    public static TestApplication Instance => _instance;
    static TestApplication _instance = null;

    public static TestApplication Create()
    {
        if (_instance == null)
        {
            _instance = Singleton.CreateFromResources<ApplicationManager, TestApplication>("Devian/Bootstrap");
        }
        return _instance;
    }
    
    public IReadOnlyList<string> patchList => _patchList;
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

    protected override async Task OnBootProc()
    {
        await base.OnBootProc();
        
        await UnityCoroutineRunner.RunAsync(this, DownloadManager.Instance.PatchProc(
            patchList,
            onDone: (patch) =>
            {
                Debug.Log(patch.TotalSize);
            }
        ));

        // await UnityCoroutineRunner.RunAsync(this, DownloadManager.Instance.DownloadProc(
        //     patchList,
        //     onProgress: (progress) =>
        //     {
        //     },
        //     onSuccess: () =>
        //     {
        //     }
        // ));
        
#if UNITY_EDITOR
        await TableManager.Instance.LoadTablesAsync("table-ndjson", TableFormat.Json);
        await TableManager.Instance.LoadStringsAsync("string-ndjson", TableFormat.Json, SystemLanguage.Korean);
#else
        await TableManager.Instance.LoadTablesAsync("table-pb64", TableFormat.Pb64);
        await TableManager.Instance.LoadStringsAsync("string-pb64", TableFormat.Pb64, SystemLanguage.Korean);
#endif

        await UnityCoroutineRunner.RunAsync(this, AssetManager.LoadBundleAssets<GameObject>("common-effects"));
        await UnityCoroutineRunner.RunAsync(this, AssetManager.LoadBundleAssets<GameObject>("prefabs"));
        await UnityCoroutineRunner.RunAsync(this, SoundManager.Instance.LoadByBundleKeyAsync("sounds"));

        //await UnityCoroutineRunner.RunAsync(this, VoiceManager.Instance.LoadByBundleKeyAsync("", SystemLanguage.Korean, SystemLanguage.English));
        var adResult = await AdManager.Instance.InitializeAsync(CancellationToken.None);
        if (adResult.IsFailure)
        {
            Debug.LogWarning($"{adResult.Error.Code}: {adResult.Error.Message}");
        }
    }
}
