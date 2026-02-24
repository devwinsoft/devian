using UnityEngine;
using Devian;

public class TestActor : ActorObject
{
    TestInputController mTestInputController = null;

    protected override void onAwake()
    {
        base.onAwake();
        mTestInputController = RegisterController<TestInputController>();
    }

    protected override void onInit()
    {
        base.onInit();
    }

    private void Start()
    {
        Init();
    }
}
