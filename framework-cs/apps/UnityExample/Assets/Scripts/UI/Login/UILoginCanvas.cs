using UnityEngine;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using Devian;

public class UILoginCanvas : UIBaseCanvas<UILoginCanvas>
{
    public TextMeshProUGUI message;
    public GameObject buttonGuest;
    public GameObject buttonGoogle;

    protected override void onInit()
    {
        message.text = TestApplication.GetVersionCode().ToString();
    }

    public void ShowResolveFrame(SaveRecordSummary localSummary, SaveRecordSummary cloudSummary)
    {
    }

    public void ShowLoginButtons()
    {
        buttonGuest.SetActive(true);
        buttonGoogle.SetActive(true);
    }

    public void OnClick_GuestLogin()
    {
        UnityTaskRunner.Run(OnClickGuestLoginAsync, $"{nameof(UILoginCanvas)}.{nameof(OnClick_GuestLogin)}");
    }

    public void OnClick_GoogleLogin()
    {
        UnityTaskRunner.Run(OnClickGoogleLoginAsync, $"{nameof(UILoginCanvas)}.{nameof(OnClick_GoogleLogin)}");
    }

    public void OnClick_SelectLocalSummary()
    {
        UnityTaskRunner.Run(ResolveConflictAsync, SyncResolution.UseLocal, $"{nameof(UILoginCanvas)}.{nameof(ResolveConflictAsync)}");
    }
    
    public void OnClick_SelectCloudSummary()
    {
        UnityTaskRunner.Run(ResolveConflictAsync, SyncResolution.UseCloud, $"{nameof(UILoginCanvas)}.{nameof(ResolveConflictAsync)}");
    }


    private async Task OnClickGuestLoginAsync()
    {
        var login = await LoginManager.Instance.LoginAndInitializeAsync(
            LoginType.GUEST,
            CancellationToken.None);
        Debug.Log($"LoginAsync: success={login.IsSuccess}");
        if (login.IsSuccess)
        {
            var result = login.Value;
            if (result.IsForceUpdate)
            {
                message.text = $"{result.VersionResult}";
                return;
            }

            if (result.IsConflict)
            {
                message.text = "SAVEDATA_SYNC_CONFLICT";
                this.ShowResolveFrame(result.LocalSummary, result.CloudSummary);
                await SaveDataManager.Instance.ResolveConflictAsync(SyncResolution.UseLocal, CancellationToken.None);
                return;
            }

            if (result.IsRecommendUpdate)
                Debug.LogWarning($"Recommend update on guest login: {result.VersionResult}");

            await TestApplication.Instance.LoadAsync(onLoadProgress);
            await SceneTransManager.Instance.LoadSceneAsync("SceneLobby");
        }
        else
        {
            message.text = $"{login.Error.Code}";
        }
    }

    private async Task OnClickGoogleLoginAsync()
    {
        var login = await LoginManager.Instance.LoginAndInitializeAsync(
            LoginType.GOOGLE,
            CancellationToken.None);
        Debug.Log($"LoginAsync: success={login.IsSuccess}");
        if (login.IsSuccess)
        {
            var result = login.Value;
            if (result.IsForceUpdate)
            {
                message.text = $"{result.VersionResult}";
                return;
            }

            if (result.IsConflict)
            {
                message.text = "SAVEDATA_SYNC_CONFLICT";
                this.ShowResolveFrame(result.LocalSummary, result.CloudSummary);
                return;
            }

            if (result.IsRecommendUpdate)
                Debug.LogWarning($"Recommend update on google login: {result.VersionResult}");

            await TestApplication.Instance.LoadAsync(onLoadProgress);
            await SceneTransManager.Instance.LoadSceneAsync("SceneSample");
        }
        else
        {
            message.text = $"{login.Error.Code}";
        }
    }

    private async Task ResolveConflictAsync(SyncResolution syncResolution)
    {
        var login = await LoginManager.Instance.ResolveConflictAndInitializeAsync(
            syncResolution,
            CancellationToken.None);
        Debug.Log($"ResolveConflictAsync: success={login.IsSuccess}");
        if (login.IsSuccess)
        {
            var result = login.Value;
            if (result.IsForceUpdate)
            {
                message.text = $"{result.VersionResult}";
                return;
            }

            if (result.IsConflict)
            {
                message.text = "SAVEDATA_SYNC_CONFLICT";
                this.ShowResolveFrame(result.LocalSummary, result.CloudSummary);
                return;
            }

            await TestApplication.Instance.LoadAsync(onLoadProgress);
            await SceneTransManager.Instance.LoadSceneAsync("SceneSample");
        }
    }

    void onLoadProgress(float progress)
    {
        message.text = $"LOADING {Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f)}%";
    }
}
