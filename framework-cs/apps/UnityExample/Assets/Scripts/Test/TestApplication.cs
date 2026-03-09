using UnityEngine;
using System;
using System.Threading.Tasks;
using Devian;

public class TestApplication : MobileApplication
{
    public static TestApplication Instance => _instance;
    static TestApplication _instance = null;

    bool _loaded = false;
    
    public static TestApplication Create()
    {
        if (_instance == null)
        {
            _instance = Singleton.CreateFromResources<ApplicationManager, TestApplication>("Devian/Application");
        }
        return _instance;
    }
    

    protected override async Task OnBootProc()
    {
        await base.OnBootProc();
    }

    
    public async Task LoadAsync(SystemLanguage language, Action<float>? onProgress = null)
    {
        if (_loaded) return;
        _loaded = true;
        
        var patchResult = await TestBundleManager.Instance.InitializeAsync();
        if (patchResult.IsSuccess)
        {
            Debug.Log(patchResult.Value!.TotalSize);
        }
        //await TestBundleManager.Instance.DownloadAsync();
        await TestBundleManager.Instance.LoadBundlesAsync(language);
    }
}
