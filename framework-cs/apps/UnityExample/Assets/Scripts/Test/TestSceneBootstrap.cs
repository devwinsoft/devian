using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Devian;

/// <summary>
/// SceneBase에 Bootstrap 통합 로직을 추가한 클래스.
/// Awake()에서 Bootstrap 생성을 트리거하고,
/// Start()에서 BootProc 완료를 보장한 뒤 OnStart()를 호출한다.
/// </summary>
public abstract class TestSceneBootstrap : SceneBase
{
    protected override void onInitAwake()
    {
        base.onInitAwake();
        TestApplication.Create();
    }

    protected override async Task onStart()
    {
        await TestApplication.Instance.BootProc();
    }
}
