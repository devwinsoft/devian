using UnityEngine;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using Devian;

public class UICanvasLoading : UICanvas<UICanvasLoading>
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
        UnityTaskRunner.Run(OnClickGuestLoginAsync, $"{nameof(UICanvasLoading)}.{nameof(OnClick_GuestLogin)}");
    }

    public void OnClick_GoogleLogin()
    {
        UnityTaskRunner.Run(OnClickGoogleLoginAsync, $"{nameof(UICanvasLoading)}.{nameof(OnClick_GoogleLogin)}");
    }

    public void OnClick_SelectLocalSummary()
    {
        UnityTaskRunner.Run(ResolveConflictAsync, SyncResolution.UseLocal, $"{nameof(UICanvasLoading)}.{nameof(ResolveConflictAsync)}");
    }
    
    public void OnClick_SelectCloudSummary()
    {
        UnityTaskRunner.Run(ResolveConflictAsync, SyncResolution.UseCloud, $"{nameof(UICanvasLoading)}.{nameof(ResolveConflictAsync)}");
    }


    private async Task OnClickGuestLoginAsync()
    {
        var login = await LoginManager.Instance.LoginAndInitializeAsync(LoginType.GUEST, CancellationToken.None);
        Debug.Log($"LoginAsync: success={login.IsSuccess}");
        if (login.IsSuccess)
        {
            if (login.Value != null && login.Value.IsConflict)
            {
                message.text = "SAVEDATA_SYNC_CONFLICT";
                this.ShowResolveFrame(login.Value.LocalSummary, login.Value.CloudSummary);
                SaveDataManager.Instance.ResolveConflictAsync(SyncResolution.UseLocal, CancellationToken.None);
                return;
            }

            await TestApplication.Instance.LoadAsync(SystemLanguage.Korean, null);
            await SceneTransManager.Instance.LoadSceneAsync("SceneSample");
        }
        else
        {
            message.text = $"{login.Error.Code}";
        }
    }

    private async Task OnClickGoogleLoginAsync()
    {
        var login = await LoginManager.Instance.LoginAndInitializeAsync(LoginType.GOOGLE, CancellationToken.None);
        Debug.Log($"LoginAsync: success={login.IsSuccess}");
        if (login.IsSuccess)
        {
            if (login.Value != null && login.Value.IsConflict)
            {
                message.text = "SAVEDATA_SYNC_CONFLICT";
                this.ShowResolveFrame(login.Value.LocalSummary, login.Value.CloudSummary);
                return;
            }

            await TestApplication.Instance.LoadAsync(SystemLanguage.Korean, null);
            await SceneTransManager.Instance.LoadSceneAsync("SceneSample");
        }
        else
        {
            message.text = $"{login.Error.Code}";
        }
    }

    private async Task ResolveConflictAsync(SyncResolution syncResolution)
    {
        var login = await LoginManager.Instance.ResolveConflictAndInitializeAsync(syncResolution, CancellationToken.None);
        Debug.Log($"ResolveConflictAsync: success={login.IsSuccess}");
        if (login.IsSuccess)
        {
            await TestApplication.Instance.LoadAsync(SystemLanguage.Korean, null);
            await SceneTransManager.Instance.LoadSceneAsync("SceneSample");
        }
    }
}
