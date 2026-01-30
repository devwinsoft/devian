// SSOT: skills/devian-unity/30-unity-components/19-sound-domain/SKILL.md
//
// Sound 도메인 사용 흐름 샘플 코드.
// Phase 2 도메인 이관 완료 후에 바로 사용할 수 있는 "정석 사용 흐름"을 보여준다.
//
// 실행 방법:
//   TestProcess.Start()에서 `yield return SoundDomainSample.Run();`을 호출하면 실행됨.
//   기본 상태에서는 호출하지 않아도 됨 (샘플 목적).
//
// 주의:
//   - Phase 1에서는 Sound 도메인 이관 전이므로 실제 테이블 데이터가 없을 수 있음.
//   - 이 샘플은 컴파일만 통과하면 됨 (실행 시 에러 발생 가능).

#nullable enable

using System.Collections;
using UnityEngine;
using Devian;

/// <summary>
/// Sound 도메인 사용 흐름을 보여주는 샘플 코드.
/// Phase 2 이후 Sound 도메인 이관이 완료되면 이 흐름 그대로 사용 가능.
/// </summary>
public static class SoundDomainSample
{
    /// <summary>
    /// Sound 도메인 초기화 → 로딩 → 재생 → 제어 흐름을 보여준다.
    /// </summary>
    public static IEnumerator Run()
    {
        Log.Info("[SoundDomainSample] === Sound Domain Sample Start ===");

        // ============================================================
        // Step 1: 테이블 로드
        // ============================================================
        // TableManager를 통해 SOUND, VOICE 테이블을 로드한다.
        // SoundVoiceTableRegistry가 RuntimeInitializeOnLoadMethod로
        // SoundManager/VoiceManager에 델리게이트를 연결해 둔 상태이다.
        //
        // Note: Phase 1에서는 테이블이 Game 도메인에 있음.
        //       Phase 2 이후에는 Sound 도메인으로 이동됨.
        // ============================================================

        Log.Info("[SoundDomainSample] Step 1: Loading tables...");

        // 실제 프로젝트에서는 아래와 같이 호출:
        // yield return TableManager.Instance.LoadTablesAsync("table-ndjson", TableFormat.Json, "SOUND", "VOICE");

        // 샘플에서는 테이블이 이미 로드되어 있다고 가정하고 스킵
        yield return null;

        // ============================================================
        // Step 2: 문자열 로드 (자막용)
        // ============================================================
        // Voice 자막을 위해 StringTable을 로드한다.
        // VoiceManager.GetSubtitleKey()로 얻은 키를 StringTable에서 조회.
        // ============================================================

        Log.Info("[SoundDomainSample] Step 2: Loading strings for subtitles...");

        // 실제 프로젝트에서는 아래와 같이 호출:
        // yield return TableManager.Instance.LoadStringsAsync("string-pb64", TableFormat.Pb64, SystemLanguage.Korean);

        yield return null;

        // ============================================================
        // Step 3: Voice Resolve (언어별 캐시 생성)
        // ============================================================
        // VoiceManager.ResolveForLanguage()를 호출하여
        // TB_VOICE의 언어별 컬럼(clip_Korean 등)을 voice_id → sound_id 캐시로 구성.
        // 이 단계 이후로는 재생 시점에 SystemLanguage를 사용하지 않는다.
        // ============================================================

        Log.Info("[SoundDomainSample] Step 3: Resolving voice for language...");

        VoiceManager.Instance.ResolveForLanguage(SystemLanguage.Korean);

        // ============================================================
        // Step 4: 사운드 로드 (Voice 제외)
        // ============================================================
        // SoundManager.LoadByKeyAsync()는 TB_SOUND에서 key 기준으로 로드하되,
        // channel == "Voice" row는 제외한다.
        // Voice 로딩은 VoiceManager의 책임.
        // ============================================================

        Log.Info("[SoundDomainSample] Step 4: Loading sounds (excluding Voice)...");

        // 실제 프로젝트에서는 아래와 같이 호출:
        // yield return SoundManager.Instance.LoadByKeyAsync("Common");

        yield return null;

        // ============================================================
        // Step 5: Voice 로드 (group_key 기반)
        // ============================================================
        // VoiceManager.LoadByGroupKeyAsync()로 TB_VOICE.group_key 기준 로드.
        // Resolve된 sound_id들만 로드한다.
        // 내부적으로 SoundManager._loadVoiceBySoundIdsAsync() 호출.
        // ============================================================

        Log.Info("[SoundDomainSample] Step 5: Loading voice clips...");

        // 실제 프로젝트에서는 아래와 같이 호출:
        // yield return VoiceManager.Instance.LoadByGroupKeyAsync(
        //     "greet",                      // TB_VOICE.group_key
        //     SystemLanguage.Korean,        // 현재 언어
        //     SystemLanguage.English        // fallback 언어
        // );

        yield return null;

        // ============================================================
        // Step 6: 사운드 재생 (SFX)
        // ============================================================
        // SoundManager.PlaySound()는 SoundRuntimeId를 반환한다.
        // 이 ID로 재생 중인 사운드를 제어할 수 있다.
        // ============================================================

        Log.Info("[SoundDomainSample] Step 6: Playing sound...");

        // 실제 프로젝트에서는 아래와 같이 호출:
        // var sfxId = SoundManager.Instance.PlaySound("sfx_click");

        // 샘플에서는 실제 재생하지 않고 로그만 출력
        var sfxId = SoundRuntimeId.Invalid;
        Log.Info($"[SoundDomainSample] PlaySound returned: {sfxId}");

        // ============================================================
        // Step 7: Voice 재생
        // ============================================================
        // VoiceManager.PlayVoice()도 SoundRuntimeId를 반환한다.
        // 내부적으로 SoundManager.PlaySound(soundId, channelOverride: "Voice") 호출.
        // ============================================================

        Log.Info("[SoundDomainSample] Step 7: Playing voice...");

        // 실제 프로젝트에서는 아래와 같이 호출:
        // var voiceId = VoiceManager.Instance.PlayVoice("hello");

        // 샘플에서는 실제 재생하지 않고 로그만 출력
        var voiceId = SoundRuntimeId.Invalid;
        Log.Info($"[SoundDomainSample] PlayVoice returned: {voiceId}");

        // ============================================================
        // Step 8: 재생 제어 (runtime_id 기반)
        // ============================================================
        // 모든 제어는 runtime_id로 수행한다.
        // SoundPlay/AudioSource를 외부에 노출하지 않는다.
        // ============================================================

        Log.Info("[SoundDomainSample] Step 8: Controlling playback...");

        // 재생 상태 확인
        if (sfxId.IsValid && SoundManager.Instance.IsPlaying(sfxId))
        {
            Log.Info("[SoundDomainSample] SFX is playing");

            // 일시정지
            SoundManager.Instance.PauseSound(sfxId);

            // 볼륨 조절
            SoundManager.Instance.SetSoundVolume(sfxId, 0.5f);

            // 재개
            SoundManager.Instance.ResumeSound(sfxId);

            // 정지
            SoundManager.Instance.StopSound(sfxId);
        }

        if (voiceId.IsValid && VoiceManager.Instance.IsVoicePlaying(voiceId))
        {
            Log.Info("[SoundDomainSample] Voice is playing");

            // Voice 제어도 동일한 패턴
            VoiceManager.Instance.PauseVoice(voiceId);
            VoiceManager.Instance.ResumeVoice(voiceId);
            VoiceManager.Instance.StopVoice(voiceId);
        }

        // ============================================================
        // Step 9: 자막 처리
        // ============================================================
        // VoiceManager.GetSubtitleKey()로 자막 키를 얻고
        // StringTable에서 실제 자막 텍스트를 조회한다.
        // ============================================================

        Log.Info("[SoundDomainSample] Step 9: Getting subtitle...");

        // 실제 프로젝트에서는 아래와 같이 호출:
        // var subtitleKey = VoiceManager.Instance.GetSubtitleKey("hello");
        // if (!string.IsNullOrEmpty(subtitleKey))
        // {
        //     var subtitle = StringTable.Get(subtitleKey);
        //     Log.Info($"Subtitle: {subtitle}");
        // }

        // ============================================================
        // Step 10: 언로드
        // ============================================================
        // 더 이상 필요 없는 사운드는 언로드한다.
        // ============================================================

        Log.Info("[SoundDomainSample] Step 10: Unloading...");

        // SoundManager.Instance.UnloadByKey("Common");
        // VoiceManager.Instance.UnloadByGroupKey("greet");

        Log.Info("[SoundDomainSample] === Sound Domain Sample Complete ===");
    }

    /// <summary>
    /// 언어 변경 시 Voice Resolve 재수행 예시.
    /// </summary>
    public static IEnumerator ChangeLanguage(SystemLanguage newLanguage)
    {
        Log.Info($"[SoundDomainSample] Changing language to {newLanguage}...");

        // 1. 기존 Voice 언로드
        VoiceManager.Instance.UnloadAllVoiceGroups();

        // 2. 새 언어로 Resolve 재수행
        VoiceManager.Instance.ResolveForLanguage(newLanguage);

        // 3. 필요한 group_key들 다시 로드
        // yield return VoiceManager.Instance.LoadByGroupKeyAsync(
        //     "greet",
        //     newLanguage,
        //     SystemLanguage.English
        // );

        yield return null;

        Log.Info($"[SoundDomainSample] Language changed to {newLanguage}");
    }
}
