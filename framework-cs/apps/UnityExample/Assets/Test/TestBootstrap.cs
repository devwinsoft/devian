using UnityEngine;
using Devian;
using System.Collections;

public class TestBootstrap : MonoBehaviour, IDevianBootStep
{
    public int Order => 1;

    public IEnumerator Boot()
    {
        Log.SetSink(new UnityLogSink());
        DownloadManager.Load("DownloadManager");
        yield return null;
    }
}
