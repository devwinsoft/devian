using UnityEngine;
using Devian;
using System;
using System.Collections;
using System.Threading;

/// <summary>
/// BaseBootstrap 파생 예제.
/// Bootstrap.prefab에 이 컴포넌트를 추가하여 사용한다.
/// </summary>
public class TestBootstrap : MobileApplication
{
    protected override IEnumerator OnBootProc()
    {
        yield return base.OnBootProc();
    }
}
