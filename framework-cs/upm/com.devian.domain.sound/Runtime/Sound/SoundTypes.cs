// SSOT: skills/devian-unity/30-unity-components/17-sound-manager/SKILL.md

#nullable enable

namespace Devian
{
    /// <summary>
    /// 사운드 채널 타입. 채널별로 독립적인 볼륨/풀/쿨타임 관리.
    /// </summary>
    public enum SoundChannelType
    {
        Bgm = 0,
        Effect = 1,
        Ui = 2,
        Voice = 3,
        Max = 4
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
    /// 오디오 row 공통 인터페이스.
    /// SOUND와 VOICE가 공통으로 구현하며, BaseAudioManager에서 재생 시 사용한다.
    /// VOICE는 isBundle/channel/loop/volume_scale/pitch_*를 상수로 반환한다.
    /// key_group은 제거됨 - 로드/언로드는 key_bundle 단위로만 수행.
    /// </summary>
    public interface IAudioRowBase
    {
        /// <summary>로드/언로드 단위 키 (Bundle label 또는 Resource 그룹 키)</summary>
        string key_bundle { get; }

        /// <summary>번들 로드 여부 (true=Bundle, false=Resource). VOICE는 항상 true.</summary>
        bool isBundle { get; }

        /// <summary>채널 타입. VOICE는 항상 Voice.</summary>
        SoundChannelType channel { get; }

        /// <summary>루프 여부. VOICE는 항상 false.</summary>
        bool loop { get; }

        /// <summary>쿨타임 (초)</summary>
        float cooltime { get; }

        /// <summary>3D 여부</summary>
        bool is3d { get; }

        /// <summary>3D near 거리 (minDistance)</summary>
        float distance_near { get; }

        /// <summary>3D far 거리 (maxDistance)</summary>
        float distance_far { get; }

        /// <summary>볼륨 스케일. VOICE는 1f.</summary>
        float volume_scale { get; }

        /// <summary>피치 최소값. VOICE는 1f.</summary>
        float pitch_min { get; }

        /// <summary>피치 최대값. VOICE는 1f.</summary>
        float pitch_max { get; }
    }

    /// <summary>
    /// TB_SOUND row 인터페이스. IAudioRowBase를 확장한다.
    /// </summary>
    public interface ISoundRow : IAudioRowBase
    {
        /// <summary>논리 사운드 ID (그룹 키, 중복 허용)</summary>
        string sound_id { get; }

        /// <summary>PK (고유)</summary>
        int row_id { get; }

        /// <summary>에셋 경로</summary>
        string path { get; }

        /// <summary>랜덤 선택 가중치 (SOUND 전용)</summary>
        int weight { get; }
    }

    /// <summary>
    /// TB_VOICE row 인터페이스. IAudioRowBase를 확장한다.
    /// 언어별 clip_ 컬럼은 TryGetClipColumn으로 접근한다 (Resolve 단계에서만 호출).
    /// VOICE defaults: isBundle=true, channel=Voice, loop=false, volume_scale=1, pitch_min=1, pitch_max=1
    /// </summary>
    public interface IVoiceRow : IAudioRowBase
    {
        /// <summary>PK (고유)</summary>
        string voice_id { get; }

        /// <summary>
        /// 컬럼명으로 clip 경로를 조회한다 (Resolve 단계에서만 호출).
        /// 재생 시점에는 절대 호출되지 않는다.
        /// </summary>
        /// <param name="columnName">컬럼명 (예: "clip_Korean", "clip_English")</param>
        /// <param name="clipPath">조회된 clip 경로</param>
        /// <returns>유효한 값이 있으면 true</returns>
        bool TryGetClipColumn(string columnName, out string clipPath);
    }
}
