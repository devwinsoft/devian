using System;
using UnityEngine;
using Devian;

public class TestActor : BaseActor
{
    protected override void onAwake()
    {
        base.onAwake();
        RegisterController<TestInputController>();
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
