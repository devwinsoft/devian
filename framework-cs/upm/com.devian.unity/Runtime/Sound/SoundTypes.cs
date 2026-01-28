// SSOT: skills/devian-unity/30-unity-components/17-sound-manager/SKILL.md

#nullable enable

namespace Devian
{
    /// <summary>
    /// 사운드 채널 타입. 채널별로 독립적인 볼륨/풀/쿨타임 관리.
    /// </summary>
    public enum SoundChannelType
    {
        Bgm,
        Effect,
        Ui,
        Voice,
        Max
    }

    /// <summary>
    /// 사운드 로딩 소스 타입.
    /// </summary>
    public enum SoundSourceType
    {
        Resource,
        Bundle
    }

    /// <summary>
    /// TB_SOUND row 인터페이스. 프로젝트에서 concrete class를 구현한다.
    /// </summary>
    public interface ISoundRow
    {
        string sound_id { get; }
        int row_id { get; }
        string key { get; }
        SoundSourceType source { get; }
        string bundle_key { get; }
        string path { get; }
        string channel { get; }
        bool loop { get; }
        float cooltime { get; }
        bool is3d { get; }
        float area_close { get; }
        float area_far { get; }

        // Optional columns (nullable for projects that don't use them)
        int weight { get; }
        float volume_scale { get; }
        float pitch_min { get; }
        float pitch_max { get; }
    }

    /// <summary>
    /// TB_VOICE row 인터페이스. 프로젝트에서 concrete class를 구현한다.
    /// 언어별 clip_ 컬럼은 concrete class에서 직접 접근한다.
    /// </summary>
    public interface IVoiceRow
    {
        string voice_id { get; }
        string text_l10n_key { get; }

        // Optional columns
        string speaker { get; }
        string category { get; }
        int priority { get; }
        string group_key { get; }
        float cooltime { get; }

        /// <summary>
        /// 지정된 언어에 해당하는 sound_id를 반환한다.
        /// 언어별 컬럼(clip_Korean, clip_English 등)에서 값을 읽는다.
        /// </summary>
        string GetSoundIdForLanguage(UnityEngine.SystemLanguage language);
    }
}
