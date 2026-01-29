using UnityEngine;
using UnityEngine.AddressableAssets;
using Devian;
using Devian.Domain.Common;
using Devian.Domain.Game;
using System.Collections;
using Unity.VisualScripting;

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
        Log.SetSink(new UnityLogSink());
        DownloadManager.Load("DownloadManager");

        yield return DownloadManager.Instance.PatchProc(
            (patch) =>
            {
                Debug.Log(patch.TotalSize);
            }
        );

        yield return TableManager.Instance.LoadTablesAsync("table-ndjson", TableFormat.Json);
        yield return TableManager.Instance.LoadStringsAsync("string-pb64", TableFormat.Pb64, SystemLanguage.Korean);
        yield return AssetManager.LoadBundleAssets<GameObject>("prefabs");

        //yield return SoundManager.Instance.LoadByKeyAsync("");
        //yield return VoiceManager.Instance.LoadByGroupKeyAsync("", SystemLanguage.Korean, SystemLanguage.English);

        Log.Debug(ST_UIText.Get("loading"));
        var obj = BundlePool.Spawn<TestPoolObject>("Cube", Vector3.zero, Quaternion.identity, null);
        Debug.Log(obj);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
