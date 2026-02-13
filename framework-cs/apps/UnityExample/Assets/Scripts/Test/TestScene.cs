using UnityEngine;
using System.Collections;
using System.Threading;
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
    public CBigInt bigInt;

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

    async void _tryActivateGpgsSavedGames()
    {
        var r = await CloudSaveManager.Instance.InitializeAsync(CancellationToken.None);
        Debug.Log($"CloudSave result: success={r.Value == CloudSaveResult.Success}");
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

        _tryActivateGpgsSavedGames();

        yield return TableManager.Instance.LoadTablesAsync("table-ndjson", TableFormat.Json);
        yield return Devian.TableManager.Instance.LoadStringsAsync(
            "string-pb64",
            Devian.TableFormat.Pb64,
            UnityEngine.SystemLanguage.Korean);
        
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
        var load_result = LocalSaveManager.Instance.LoadPayload("main");

        CBigInt x = new CBigInt(12345, 1);
        Debug.Log(x * bigInt);
    }

    public override IEnumerator OnExit()
    {
        yield return null;
    }
}
