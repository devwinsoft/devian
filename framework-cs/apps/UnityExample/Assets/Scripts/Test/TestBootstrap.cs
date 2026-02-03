using UnityEngine;
using Devian;
using System;
using System.Collections;

/// <summary>
/// BaseBootstrap 파생 예제.
/// Bootstrap.prefab에 이 컴포넌트를 추가하여 사용한다.
/// </summary>
public class TestBootstrap : BaseBootstrap
{
    /// <summary>
    /// 외부에서 BootProc에 로직을 주입할 수 있는 이벤트.
    /// </summary>
    public static event Action? BootProcInjected;

    protected override IEnumerator OnBootProc()
    {
        Log.SetSink(new UnityLogSink());

        // 외부 주입 로직 실행
        BootProcInjected?.Invoke();

        yield return null;
    }
}
