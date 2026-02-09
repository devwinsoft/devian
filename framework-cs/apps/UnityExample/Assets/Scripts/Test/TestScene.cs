using UnityEngine;
using System.Collections;
using Devian;
using Devian.Domain.Common;
using Devian.Domain.Game;
using Devian.Domain.Sound;
using Devian.Protocol.Game;
using TMPro;
using Devian;

public class TestScene : BaseScene
{
    public CInt a;
    public CFloat b;
    public CString c;
    public COMPLEX_POLICY_ID policyId;
    public TestSheet_ID testSheetId;
    public COMMON_EFFECT_ID effectId;
    public SOUND_ID soundId;
    public VOICE_ID voiceId;
    public TEXT_ID textID;

    protected override void OnInitAwake()
    {
        base.OnInitAwake();
        Debug.Log("[SceneTest::OnInitAwake]");
    }

    public override IEnumerator OnEnter()
    {
        Debug.Log("[SceneTest::OnEnter]");
        yield return null;
    }

    public override IEnumerator OnStart()
    {
        yield return base.OnStart();

        yield return DownloadManager.Instance.PatchProc(
            (patch) =>
            {
                Debug.Log(patch.TotalSize);
            }
        );

        yield return TableManager.Instance.LoadTablesAsync("table-ndjson", TableFormat.Json);
        yield return TableManager.Instance.LoadStringsAsync("string-pb64", TableFormat.Pb64, SystemLanguage.Korean);

        yield return SoundManager.Instance.LoadByBundleKeyAsync("sounds");
        yield return AssetManager.LoadBundleAssets<GameObject>("common-effects");
        yield return AssetManager.LoadBundleAssets<GameObject>("prefabs");

        //yield return VoiceManager.Instance.LoadByGroupKeyAsync("", SystemLanguage.Korean, SystemLanguage.English);

        TestUICanvas.Instance.Init();
        SoundManager.Instance.PlaySound("bgm_title");
        CommonEffectManager.Instance.CreateEffect(effectId, null, Vector3.zero, Quaternion.identity, COMMON_EFFECT_ATTACH_TYPE.World);

        Log.Debug(ST_TEXT.Get("loading"));
        var obj = BundlePool.Spawn<TestPoolObject>("Cube", Vector3.zero, Quaternion.identity, null);
        Debug.Log(obj);

        var save_result = LocalSaveManager.Instance.Save("main", "ABCD");
        Debug.Log(save_result.Value);
        var load_result = LocalSaveManager.Instance.LoadPayload("main");
        Debug.Log(load_result.Value);
        Debug.Log(Application.persistentDataPath);
    }

    public override IEnumerator OnExit()
    {
        yield return null;
    }
}
