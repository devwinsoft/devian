using UnityEngine;
using System.Threading.Tasks;
using Devian;
using Devian.Domain.Common;
using Devian.Domain.Game;
using Devian.Domain.Sound;
using Devian.Protocol.Game;
using NUnit.Framework.Internal;
using TMPro;

public class SceneSample : SceneBootstrap
{
    public VersionNumber version;
    public CInt a;
    public CFloat b;
    public CString c;
    public COMPLEX_POLICY_ID policyId;
    public COMMON_EFFECT_ID effectId;
    public SOUND_ID soundId;
    public VOICE_ID voiceId;
    public TEXT_ID textID;
    public CBigInt bigInt;
    

    protected override Task onEnter()
    {
        return Task.CompletedTask;
    }

    protected override async Task onStart()
    {
        Debug.Log("TestSceneSample...");
        await base.onStart();

        UICanvasSample.Instance.Init();
        
        SoundManager.Instance.PlaySound("bgm_title");
        CommonEffectManager.Instance.CreateEffect(effectId, null, Vector3.zero, Quaternion.identity, COMMON_EFFECT_ATTACH_TYPE.World);

        Log.Debug(ST_TEXT.Get("loading"));
        BundlePool.Spawn<TestPoolObject>("Cube", Vector3.zero, Quaternion.identity, null);

        foreach (var key in InventoryManager.Instance.Storage.Rentals.Keys)
        {
            Debug.Log($"Rentals: {key}");
        }
        foreach (var key in InventoryManager.Instance.Storage.Passes.Keys)
        {
            Debug.Log($"Passes: {key}");
        }
    }

    protected override Task onExit()
    {
        return Task.CompletedTask;
    }
}
