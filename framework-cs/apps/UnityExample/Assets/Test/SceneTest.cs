using UnityEngine;
using Devian;
using System.Collections;

public class SceneTest : BaseScene
{
    protected override void OnInitAwake()
    {
        Debug.Log("[SceneTest::OnInitAwake]");
    }

    public override IEnumerator OnEnter()
    {
        Debug.Log("[SceneTest::OnEnter]");
        yield return null;
    }

    public override IEnumerator OnExit()
    {
        yield return null;
    }
}
