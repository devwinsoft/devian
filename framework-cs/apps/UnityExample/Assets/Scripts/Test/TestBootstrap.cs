using UnityEngine;
using Devian;
using System.Collections;

public class TestBootstrap : BootSingleton<TestBootstrap>
{
    public int Order => 1;

    public IEnumerator Boot()
    {
        Log.SetSink(new UnityLogSink());
        yield return null;
    }
}
