using UnityEngine;
using Devian;
using System;
using System.Collections;
using System.Threading;

/// <summary>
/// BaseBootstrap 파생 예제.
/// Bootstrap.prefab에 이 컴포넌트를 추가하여 사용한다.
/// </summary>
public class TestBootstrap : BaseBootstrap
{
    protected override IEnumerator OnBootProc()
    {
        Log.SetSink(new UnityLogSink());
        
#if UNITY_ANDROID && !UNITY_EDITOR
        // GPGS 플러그인을 프로젝트에 설치했다는 전제
        // (이 코드는 GPGS 미설치면 컴파일 에러 나니까, 설치한 프로젝트에서만 사용)
        GooglePlayGames.PlayGamesPlatform.Activate();
#endif
        yield break;
    }
}
