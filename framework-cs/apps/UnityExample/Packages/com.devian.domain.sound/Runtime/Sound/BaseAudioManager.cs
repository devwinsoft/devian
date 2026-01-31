// SSOT: skills/devian-unity/30-unity-components/20-base-audio-manager/SKILL.md

#nullable enable

using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Sound/Voice 공통 오디오 재생 로직.
    /// 볼륨/피치 계산, 3D 설정 파라미터 구성을 담당한다.
    /// SoundManager/VoiceManager가 공통으로 호출한다.
    /// </summary>
    internal static class BaseAudioManager
    {
        /// <summary>
        /// 볼륨/피치/3D 파라미터를 계산한다.
        /// </summary>
        /// <param name="row">IAudioRowBase (SOUND 또는 VOICE)</param>
        /// <param name="externalVolume">외부 볼륨 스케일 (0~1)</param>
        /// <param name="pitchOverride">피치 오버라이드 (0이면 row 기반 랜덤)</param>
        /// <param name="position">3D 위치 (null이면 2D)</param>
        /// <param name="finalVolume">계산된 최종 볼륨</param>
        /// <param name="finalPitch">계산된 최종 피치</param>
        /// <param name="effective3d">실제 3D 적용 여부</param>
        public static void ComputePlayParams(
            IAudioRowBase row,
            float externalVolume,
            float pitchOverride,
            Vector3? position,
            out float finalVolume,
            out float finalPitch,
            out bool effective3d)
        {
            // 볼륨: externalVolume * row.volume_scale
            finalVolume = externalVolume * row.volume_scale;

            // 피치: pitchOverride가 0이 아니면 사용, 아니면 row 기반
            if (pitchOverride != 0f)
            {
                finalPitch = pitchOverride;
            }
            else if (row.pitch_min > 0f && row.pitch_max > 0f && row.pitch_min < row.pitch_max)
            {
                // 랜덤 피치
                finalPitch = Random.Range(row.pitch_min, row.pitch_max);
            }
            else if (row.pitch_min > 0f)
            {
                // min == max 또는 max가 0인 경우 고정
                finalPitch = row.pitch_min;
            }
            else
            {
                finalPitch = 1f;
            }

            // 3D 판정: row.is3d && position.HasValue
            effective3d = row.is3d && position.HasValue;
        }

        /// <summary>
        /// SoundChannel에 재생 요청을 위임한다.
        /// </summary>
        /// <param name="channel">재생할 채널</param>
        /// <param name="runtimeId">runtime_id</param>
        /// <param name="logicalId">논리 ID (Sound: soundId, Voice: voiceId)</param>
        /// <param name="rowId">row_id (Voice는 0 또는 voice_id 해시)</param>
        /// <param name="row">IAudioRowBase</param>
        /// <param name="clip">AudioClip</param>
        /// <param name="externalVolume">외부 볼륨</param>
        /// <param name="pitchOverride">피치 오버라이드 (0이면 row 기반)</param>
        /// <param name="groupId">그룹 ID</param>
        /// <param name="position">3D 위치 (null이면 2D)</param>
        /// <returns>재생 성공 여부</returns>
        public static bool TryPlay(
            SoundChannel channel,
            SoundRuntimeId runtimeId,
            string logicalId,
            int rowId,
            IAudioRowBase row,
            AudioClip clip,
            float externalVolume,
            float pitchOverride,
            int groupId,
            Vector3? position)
        {
            ComputePlayParams(row, externalVolume, pitchOverride, position, out var finalVolume, out var finalPitch, out var effective3d);

            return channel.PlayWithRuntimeId(
                runtimeId,
                logicalId,
                rowId,
                clip,
                finalVolume,
                row.loop,
                row.cooltime,
                fadeInSeconds: 0f,
                fadeOutSeconds: 0f,
                waitSeconds: 0f,
                groupId,
                finalPitch,
                effective3d,
                position,
                row.distance_near,
                row.distance_far
            );
        }
    }
}
