using UnityEngine;
using System.Collections;
using Devian;
using Devian.Domain.Common;
using Devian.Domain.Game;
using Devian.Domain.Sound;

public class SceneTest : BaseScene
{
    public CInt a;
    public CFloat b;
    public CString c;
    public COMPLEX_POLICY_ID policyId;
    public TestSheet_ID testSheetId;
    public COMMON_EFFECT_ID effectId;
    public SOUND_ID soundId;
    public VOICE_ID voiceId;
    public UIText_ID textID;

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

        SoundManager.Instance.PlaySound("bgm_title");
        CommonEffectManager.Instance.CreateEffect(effectId, null, Vector3.zero, Quaternion.identity, COMMON_EFFECT_ATTACH_TYPE.World);

        Log.Debug(ST_UIText.Get("loading"));
        var obj = BundlePool.Spawn<TestPoolObject>("Cube", Vector3.zero, Quaternion.identity, null);
        Debug.Log(obj);
    }

    public override IEnumerator OnExit()
    {
        yield return null;
    }
}
