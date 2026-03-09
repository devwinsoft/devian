using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Devian;

public class SceneLoading : SceneBootstrap
{
    public static SceneLoading Instance => Singleton.Create<SceneLoading>();

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
        Debug.Log("TestSceneLoading...");
        Debug.Log(Application.persistentDataPath);
        await base.onStart();

        UICanvasLoading.Instance.Init();

        var initialize = await LoginManager.Instance.EnsureRuntimeSessionAndInitializeAsync(CancellationToken.None);
        Debug.Log($"EnsureRuntimeSessionAndInitializeAsync: success={initialize.IsSuccess}");
        if (initialize.IsFailure)
        {
            Debug.LogError($"Login bootstrap failed: code={initialize.Error.Code}, message={initialize.Error.Message}");
            UICanvasLoading.Instance.message.text = $"{initialize.Error.Code}";
            UICanvasLoading.Instance.ShowLoginButtons();
            return;
        }

        if (initialize.Value == null)
        {
            Debug.Log("Login bootstrap pending explicit login.");
            UICanvasLoading.Instance.ShowLoginButtons();
            return;
        }

        if (initialize.Value.IsConflict)
        {
            Debug.LogWarning(
                $"Login bootstrap conflict: local={initialize.Value.LocalDeviceId}, cloud={initialize.Value.CloudDeviceId}");
            UICanvasLoading.Instance.message.text = "SAVEDATA_SYNC_CONFLICT";
            UICanvasLoading.Instance.ShowResolveFrame(initialize.Value.LocalSummary, initialize.Value.CloudSummary);
            return;
        }

        await TestApplication.Instance.LoadAsync(SystemLanguage.Korean, null);
        await SceneTransManager.Instance.LoadSceneAsync("SceneSample");
    }
}
