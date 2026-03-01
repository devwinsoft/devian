using System;
using UnityEngine;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using Devian;
using Devian.Domain.Common;

public class UICanvasLoading : UICanvas<UICanvasLoading>
{
    public TextMeshProUGUI message;
    public GameObject buttonGuest;
    public GameObject buttonGoogle;

    protected override void onInit()
    {
        message.text = TestApplication.GetVersionCode().ToString();
    }

    public void ShowLoginButtons()
    {
        buttonGuest.SetActive(true);
        buttonGoogle.SetActive(true);
    }

    public void OnClick_GuestLogin()
    {
        runUiTask(OnClickGuestLoginAsync(), nameof(OnClick_GuestLogin));
    }

    public void OnClick_GoogleLogin()
    {
        runUiTask(OnClickGoogleLoginAsync(), nameof(OnClick_GoogleLogin));
    }

    private async Task OnClickGuestLoginAsync()
    {
        var code = await TestSceneLoading.Instance.LoginSessionAsync(LoginType.GUEST);
        Debug.Log($"LoginAsync: {code}");
        if (code == CommonErrorType.SUCCESS)
        {
            await SceneTransManager.Instance.LoadSceneAsync("SceneSample");
        }
        else
        {
            message.text = $"{code}";
        }
    }

    private async Task OnClickGoogleLoginAsync()
    {
        var code = await TestSceneLoading.Instance.LoginSessionAsync(LoginType.GOOGLE);
        Debug.Log($"LoginAsync: {code}");
        if (code == CommonErrorType.SUCCESS)
        {
            await SceneTransManager.Instance.LoadSceneAsync("SceneSample");
        }
        else
        {
            message.text = $"{code}";
        }
    }

    private void runUiTask(Task task, string operation)
    {
        _ = observeUiTaskAsync(task, operation);
    }

    private static async Task observeUiTaskAsync(Task task, string operation)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            Debug.LogError($"UICanvasLoading.{operation} failed: {ex}");
        }
    }
}
