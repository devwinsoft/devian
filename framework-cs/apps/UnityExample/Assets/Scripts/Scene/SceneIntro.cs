using System.Threading.Tasks;
using UnityEngine;
using Devian;

public class SceneIntro : SceneBase
{
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
        await Task.Delay(1000);
        SceneTransManager.Instance.LoadSceneAsync("SceneLogin");
    }
}
