using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Devian;
using Devian.Domain.Game;

public class SceneLogin : SceneBootstrap
{
    public static SceneLogin Instance => singleton;
    static SceneLogin singleton = null;

    protected override void onInitAwake()
    {
        base.onInitAwake();
        singleton = this;
    }

    protected override void onDestroy()
    {
        base.onDestroy();
        singleton = null;
    }

    protected override Task onEnter()
    {
        return Task.CompletedTask;
    }

    protected override Task onExit()
    {
        return Task.CompletedTask;
    }

    protected override async Task onStart()
    {
        Debug.Log("SceneLogin.onStart()...");
        Debug.Log(Application.persistentDataPath);
        await base.onStart();

        UILoginCanvas.Instance.Init();

        // Load assets...
        await TestApplication.Instance.LoadAsync(
            progress =>
            {
                UILoginCanvas.Instance.message.text = $"LOADING {Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f)}%";
            });
        
        // Auto login...
        var initialize = await LoginManager.Instance.EnsureRuntimeSessionAndInitializeAsync(
            TestApplication.Instance.AppVersion,
            CancellationToken.None);
        Debug.Log($"EnsureRuntimeSessionAndInitializeAsync: success={initialize.IsSuccess}");
        if (initialize.IsFailure)
        {
            Debug.LogError($"Login bootstrap failed: code={initialize.Error.Code}, message={initialize.Error.Message}");
            UILoginCanvas.Instance.message.text = $"{initialize.Error.Code}";
            UILoginCanvas.Instance.ShowLoginButtons();
            return;
        }

        var result = initialize.Value;
        if (result.IsForceUpdate)
        {
            Debug.LogWarning($"Login bootstrap blocked by force update: {result.VersionResult}");
            UILoginCanvas.Instance.message.text = $"{result.VersionResult}";
            return;
        }

        if (!AccountManager.Instance.HasAuthenticatedSession)
        {
            Debug.Log($"Login bootstrap pending explicit login by auth state. version={result.VersionResult}");
            if (result.IsRecommendUpdate)
                UILoginCanvas.Instance.message.text = $"{result.VersionResult}";

            UILoginCanvas.Instance.ShowLoginButtons();
            return;
        }
        
        if (result.IsConflict)
        {
            Debug.LogWarning(
                $"Login bootstrap conflict: local={result.LocalDeviceId}, cloud={result.CloudDeviceId}");
            UILoginCanvas.Instance.message.text = "SAVEDATA_SYNC_CONFLICT";
            UILoginCanvas.Instance.ShowResolveFrame(result.LocalSummary, result.CloudSummary);
            return;
        }

        if (result.IsRecommendUpdate)
            Debug.LogWarning($"Login bootstrap recommend update: {result.VersionResult}");

        if (result.IsInitial)
        {
            UILoginCanvas.Instance.ShowLoginButtons();
            return;
        }

        await SceneTransManager.Instance.LoadSceneAsync("SceneSample");
    }
}
