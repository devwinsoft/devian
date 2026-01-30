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
    /// 사운드 런타임 ID.
    /// PlaySound/PlayVoice가 반환하며, 이후 모든 제어(Stop/Pause/Resume 등)는 이 ID로 수행한다.
    /// </summary>
    public readonly struct SoundRuntimeId
    {
        public readonly int Value;

        public SoundRuntimeId(int value) => Value = value;

        /// <summary>유효한 ID인지 확인 (0 이하는 무효)</summary>
        public bool IsValid => Value > 0;

        /// <summary>무효한 ID (재생 실패 시 반환)</summary>
        public static SoundRuntimeId Invalid => new(0);

        public override string ToString() => $"SoundRuntimeId({Value})";
        public override int GetHashCode() => Value;
        public override bool Equals(object? obj) => obj is SoundRuntimeId other && Value == other.Value;

        public static bool operator ==(SoundRuntimeId left, SoundRuntimeId right) => left.Value == right.Value;
        public static bool operator !=(SoundRuntimeId left, SoundRuntimeId right) => left.Value != right.Value;
    }

    /// <summary>
    /// 재생 중인 사운드 정보 (readonly, 디버그/툴용).
    /// </summary>
    public readonly struct PlayingInfo
    {
        public readonly SoundRuntimeId RuntimeId;
        public readonly string SoundId;
        public readonly int RowId;
        public readonly SoundChannelType Channel;
        public readonly float StartTime;
        public readonly bool Loop;
        public readonly float Volume;
        public readonly float Pitch;
        public readonly bool IsPaused;

        public PlayingInfo(
            SoundRuntimeId runtimeId,
            string soundId,
            int rowId,
            SoundChannelType channel,
            float startTime,
            bool loop,
            float volume,
            float pitch,
            bool isPaused)
        {
            RuntimeId = runtimeId;
            SoundId = soundId;
            RowId = rowId;
            Channel = channel;
            StartTime = startTime;
            Loop = loop;
            Volume = volume;
            Pitch = pitch;
            IsPaused = isPaused;
        }
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

        // 3D 파라미터 (SSOT: 20-base-audio-manager)
        float distance_near { get; }  // 3D near 거리 (minDistance)
        float distance_far { get; }   // 3D far 거리 (maxDistance)

        // 재생 파라미터
        int weight { get; }           // 랜덤 선택 가중치
        float volume_scale { get; }   // 볼륨 스케일
        float pitch_min { get; }      // 피치 랜덤 최소
        float pitch_max { get; }      // 피치 랜덤 최대
    }

    /// <summary>
    /// TB_VOICE row 인터페이스. 프로젝트에서 concrete class를 구현한다.
    /// 언어별 clip_ 컬럼은 TryGetClipColumn으로 접근한다 (Resolve 단계에서만 호출).
    /// text_l10n_key 제거됨 - 자막 키가 필요하면 voice_id 자체를 사용
    /// </summary>
    public interface IVoiceRow
    {
        string voice_id { get; }

        // Optional columns
        string speaker { get; }
        string category { get; }
        int priority { get; }
        string group_key { get; }
        float cooltime { get; }

        /// <summary>
        /// 컬럼명으로 sound_id를 조회한다 (Resolve 단계에서만 호출).
        /// 재생 시점에는 절대 호출되지 않는다.
        /// </summary>
        /// <param name="columnName">컬럼명 (예: "clip_Korean", "clip_English")</param>
        /// <param name="soundId">조회된 sound_id</param>
        /// <returns>유효한 값이 있으면 true</returns>
        bool TryGetClipColumn(string columnName, out string soundId);
    }
}
