using UnityEngine;
using System.Threading;
using System.Threading.Tasks;
using Devian;
using Devian.Domain.Common;

public class UICanvasLoading : UICanvas<UICanvasLoading>
{
    public GameObject buttonGuest;
    public GameObject buttonGoogle;

    public void ShowLoginButtons()
    {
        buttonGuest.SetActive(true);
        buttonGoogle.SetActive(true);
    }

    public async void OnClick_GuestLogin()
    {
        var code = await TestSceneLoading.Instance.Login(LoginType.GUEST);
        Debug.Log($"LoginAsync: {code}");
        if (code == CommonErrorType.SUCCESS)
        {
            SceneTransManager.Instance.LoadSceneAsync("SceneSample");
        }
    }

    public async void OnClick_GoogleLogin()
    {
        var code = await TestSceneLoading.Instance.Login(LoginType.GOOGLE);
        Debug.Log($"LoginAsync: {code}");
        if (code == CommonErrorType.SUCCESS)
        {
            SceneTransManager.Instance.LoadSceneAsync("SceneSample");
        }
    }
    
}
