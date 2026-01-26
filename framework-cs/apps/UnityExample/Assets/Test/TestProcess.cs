using UnityEngine;
using UnityEngine.AddressableAssets;
using Devian;
using Devian.Domain.Common;
using Devian.Domain.Game;
using System.Collections;

public class NewMonoBehaviourScript : MonoBehaviour
{
    public CInt a;
    public CFloat b;
    public CString c;
    public COMPLEX_POLICY_ID policyId;
    public TestSheet_ID testSheetId;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    IEnumerator Start()
    {
        Devian.Logger.SetSink(new UnityLogSink());
        DownloadManager.Load("DownloadManager");
        yield return DownloadManager.Instance.PatchProc(
            (patch) =>
            {
                Debug.Log(patch.TotalSize);
            }
        );

        yield return AssetManager.LoadBundleAssets<GameObject>("prefabs");
        var obj = BundlePool.Spawn<TestPoolObject>("Cube", Vector3.zero, Quaternion.identity, null);
        Debug.Log(obj);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
