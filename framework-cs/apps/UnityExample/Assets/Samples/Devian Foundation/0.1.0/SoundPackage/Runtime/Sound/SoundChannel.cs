// SSOT: skills/devian-unity/22-sound-system/17-sound-manager/SKILL.md

#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// 사운드 채널. 풀/재생 리스트/쿨타임/SEQ를 관리한다.
    /// SoundManager는 AudioSource를 직접 들지 않고 SoundChannel에 위임한다.
    /// runtime_id 기반 제어를 지원한다.
    /// </summary>
    public sealed class SoundChannel : MonoBehaviour
    {
        private SoundChannelType _channelType;
        private readonly List<SoundPlay> _pool = new();
        private readonly List<SoundPlay> _activePlays = new();
        private readonly Dictionary<string, float> _cooldowns = new();

        // runtime_id → (SoundPlay, generation) 매핑
        private readonly Dictionary<int, (SoundPlay play, int generation)> _playingByRuntimeId = new();

        // soundId → runtime_id 집합 (StopAllBySoundId용)
        private readonly Dictionary<string, HashSet<int>> _runtimeIdsBySoundId = new();

        private float _volume = 1f;
        private bool _isMuted;
        private int _poolSize = 4;

        // 종료 콜백 (SoundManager에게 알림)
        public Action<SoundRuntimeId>? OnPlayFinished { get; set; }

        public SoundChannelType ChannelType => _channelType;
        public float Volume => _volume;
        public bool IsMuted => _isMuted;
        public int ActiveCount => _activePlays.Count;

        /// <summary>
        /// 채널을 초기화한다.
        /// </summary>
        public void Initialize(SoundChannelType type, int poolSize = 4)
        {
            _channelType = type;
            _poolSize = poolSize;

            // 초기 풀 생성
            for (int i = 0; i < _poolSize; i++)
            {
                _createPooledPlay();
            }
        }

        private SoundPlay _createPooledPlay()
        {
            var go = new GameObject($"SoundPlay_{_channelType}_{_pool.Count}");
            go.transform.SetParent(transform);
            var play = go.AddComponent<SoundPlay>();
            go.SetActive(false);
            _pool.Add(play);
            return play;
        }

        private SoundPlay _getFromPool()
        {
            // 유휴 상태인 것 찾기
            foreach (var play in _pool)
            {
                if (!play.IsActive)
                {
                    return play;
                }
            }

            // 풀이 부족하면 확장
            return _createPooledPlay();
        }

        /// <summary>
        /// 사운드를 재생하고 runtime_id를 등록한다.
        /// </summary>
        /// <param name="runtimeId">SoundManager가 발급한 runtime_id</param>
        /// <param name="soundId">논리 사운드 ID</param>
        /// <param name="rowId">TB_SOUND row_id</param>
        /// <param name="clip">재생할 AudioClip</param>
        /// <returns>재생 시작 성공 여부</returns>
        public bool PlayWithRuntimeId(
            SoundRuntimeId runtimeId,
            string soundId,
            int rowId,
            AudioClip clip,
            float volume = 1f,
            bool loop = false,
            float cooltime = 0f,
            float fadeInSeconds = 0f,
            float fadeOutSeconds = 0f,
            float waitSeconds = 0f,
            int groupId = 0,
            float pitch = 1f,
            bool is3d = false,
            Vector3? position = null,
            float areaClose = 1f,
            float areaFar = 500f)
        {
            // 쿨타임 체크
            if (cooltime > 0f && _isOnCooldown(soundId))
            {
                return false;
            }

            var play = _getFromPool();
            play.gameObject.SetActive(true);

            // Acquire: generation 증가 + runtime 정보 설정
            play.Acquire(runtimeId, soundId, rowId);

            var effectiveVolume = _isMuted ? 0f : volume * _volume;
            play.Play(clip, effectiveVolume, loop, fadeInSeconds, fadeOutSeconds, waitSeconds, groupId, pitch, is3d, position, areaClose, areaFar);

            _activePlays.Add(play);

            // runtime_id → (play, generation) 매핑
            _playingByRuntimeId[runtimeId.Value] = (play, play.Generation);

            // soundId → runtime_id 집합
            if (!_runtimeIdsBySoundId.TryGetValue(soundId, out var idSet))
            {
                idSet = new HashSet<int>();
                _runtimeIdsBySoundId[soundId] = idSet;
            }
            idSet.Add(runtimeId.Value);

            // 쿨다운 등록
            if (cooltime > 0f)
            {
                _cooldowns[soundId] = Time.realtimeSinceStartup + cooltime;
            }

            return true;
        }

        /// <summary>
        /// runtime_id로 재생을 정지한다.
        /// </summary>
        public bool StopByRuntimeId(SoundRuntimeId runtimeId)
        {
            if (!_playingByRuntimeId.TryGetValue(runtimeId.Value, out var entry))
            {
                return false;
            }

            var (play, generation) = entry;

            // generation 검증 (풀 재사용 방지)
            if (!play.ValidateGeneration(generation))
            {
                _cleanupRuntimeId(runtimeId.Value);
                return false;
            }

            play.Stop();
            return true;
        }

        /// <summary>
        /// runtime_id로 재생을 일시정지한다.
        /// </summary>
        public bool PauseByRuntimeId(SoundRuntimeId runtimeId)
        {
            if (!_playingByRuntimeId.TryGetValue(runtimeId.Value, out var entry))
            {
                return false;
            }

            var (play, generation) = entry;

            if (!play.ValidateGeneration(generation))
            {
                _cleanupRuntimeId(runtimeId.Value);
                return false;
            }

            return play.Pause();
        }

        /// <summary>
        /// runtime_id로 일시정지를 해제한다.
        /// </summary>
        public bool ResumeByRuntimeId(SoundRuntimeId runtimeId)
        {
            if (!_playingByRuntimeId.TryGetValue(runtimeId.Value, out var entry))
            {
                return false;
            }

            var (play, generation) = entry;

            if (!play.ValidateGeneration(generation))
            {
                _cleanupRuntimeId(runtimeId.Value);
                return false;
            }

            return play.Resume();
        }

        /// <summary>
        /// runtime_id로 볼륨을 설정한다.
        /// </summary>
        public bool SetVolumeByRuntimeId(SoundRuntimeId runtimeId, float volume)
        {
            if (!_playingByRuntimeId.TryGetValue(runtimeId.Value, out var entry))
            {
                return false;
            }

            var (play, generation) = entry;

            if (!play.ValidateGeneration(generation))
            {
                _cleanupRuntimeId(runtimeId.Value);
                return false;
            }

            play.SetVolume(volume * _volume);
            return true;
        }

        /// <summary>
        /// runtime_id로 피치를 설정한다.
        /// </summary>
        public bool SetPitchByRuntimeId(SoundRuntimeId runtimeId, float pitch)
        {
            if (!_playingByRuntimeId.TryGetValue(runtimeId.Value, out var entry))
            {
                return false;
            }

            var (play, generation) = entry;

            if (!play.ValidateGeneration(generation))
            {
                _cleanupRuntimeId(runtimeId.Value);
                return false;
            }

            play.SetPitch(pitch);
            return true;
        }

        /// <summary>
        /// runtime_id로 재생 중인지 확인한다.
        /// </summary>
        public bool IsPlayingByRuntimeId(SoundRuntimeId runtimeId)
        {
            if (!_playingByRuntimeId.TryGetValue(runtimeId.Value, out var entry))
            {
                return false;
            }

            var (play, generation) = entry;

            if (!play.ValidateGeneration(generation))
            {
                _cleanupRuntimeId(runtimeId.Value);
                return false;
            }

            return play.IsActive;
        }

        /// <summary>
        /// runtime_id로 재생 정보를 조회한다.
        /// </summary>
        public bool TryGetPlayingInfo(SoundRuntimeId runtimeId, out PlayingInfo info)
        {
            info = default;

            if (!_playingByRuntimeId.TryGetValue(runtimeId.Value, out var entry))
            {
                return false;
            }

            var (play, generation) = entry;

            if (!play.ValidateGeneration(generation))
            {
                _cleanupRuntimeId(runtimeId.Value);
                return false;
            }

            info = new PlayingInfo(
                play.RuntimeId,
                play.SoundId,
                play.RowId,
                _channelType,
                play.StartTime,
                play.Loop,
                play.TargetVolume,
                play.Pitch,
                play.IsPaused
            );
            return true;
        }

        /// <summary>
        /// 특정 soundId의 모든 재생을 정지한다.
        /// </summary>
        public int StopAllBySoundId(string soundId)
        {
            if (!_runtimeIdsBySoundId.TryGetValue(soundId, out var idSet))
            {
                return 0;
            }

            int count = 0;
            var ids = new List<int>(idSet);

            foreach (var id in ids)
            {
                if (StopByRuntimeId(new SoundRuntimeId(id)))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 특정 groupId의 모든 사운드를 정지한다.
        /// </summary>
        public void StopByGroup(int groupId)
        {
            for (int i = _activePlays.Count - 1; i >= 0; i--)
            {
                var play = _activePlays[i];
                if (play.GroupId == groupId)
                {
                    play.Stop();
                }
            }
        }

        /// <summary>
        /// 이 채널의 모든 사운드를 정지한다.
        /// </summary>
        public void StopAll()
        {
            foreach (var play in _activePlays)
            {
                play.Stop();
            }
        }

        /// <summary>
        /// 이 채널의 모든 사운드를 페이드 아웃 후 정지한다.
        /// </summary>
        public void FadeOutAll(float fadeOutSeconds)
        {
            foreach (var play in _activePlays)
            {
                play.FadeOutAndStop(fadeOutSeconds);
            }
        }

        /// <summary>
        /// 볼륨을 설정한다.
        /// </summary>
        public void SetVolume(float volume)
        {
            _volume = Mathf.Clamp01(volume);
            _updateActiveVolumes();
        }

        /// <summary>
        /// 음소거 상태를 설정한다.
        /// </summary>
        public void SetMute(bool mute)
        {
            _isMuted = mute;
            _updateActiveVolumes();
        }

        private void _updateActiveVolumes()
        {
            foreach (var play in _activePlays)
            {
                if (play.AudioSource != null)
                {
                    play.AudioSource.mute = _isMuted;
                }
            }
        }

        private bool _isOnCooldown(string soundId)
        {
            if (_cooldowns.TryGetValue(soundId, out var cooldownEnd))
            {
                return Time.realtimeSinceStartup < cooldownEnd;
            }
            return false;
        }

        private void _cleanupRuntimeId(int runtimeIdValue)
        {
            if (_playingByRuntimeId.TryGetValue(runtimeIdValue, out var entry))
            {
                var soundId = entry.play.SoundId;
                if (_runtimeIdsBySoundId.TryGetValue(soundId, out var idSet))
                {
                    idSet.Remove(runtimeIdValue);
                    if (idSet.Count == 0)
                    {
                        _runtimeIdsBySoundId.Remove(soundId);
                    }
                }
                _playingByRuntimeId.Remove(runtimeIdValue);
            }
        }

        private void Update()
        {
            // 완료된 재생을 풀로 반환
            for (int i = _activePlays.Count - 1; i >= 0; i--)
            {
                var play = _activePlays[i];
                if (play.CurrentState == SoundPlay.State.Finished)
                {
                    var runtimeId = play.RuntimeId;

                    // 정리
                    _cleanupRuntimeId(runtimeId.Value);

                    play.Reset();
                    play.gameObject.SetActive(false);
                    _activePlays.RemoveAt(i);

                    // SoundManager에게 알림
                    OnPlayFinished?.Invoke(runtimeId);
                }
            }

            // 만료된 쿨다운 정리 (가끔씩)
            if (Time.frameCount % 300 == 0)
            {
                _cleanupExpiredCooldowns();
            }
        }

        private void _cleanupExpiredCooldowns()
        {
            var now = Time.realtimeSinceStartup;
            var keysToRemove = new List<string>();

            foreach (var kvp in _cooldowns)
            {
                if (kvp.Value < now)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _cooldowns.Remove(key);
            }
        }

        private void OnDestroy()
        {
            StopAll();
            _pool.Clear();
            _activePlays.Clear();
            _cooldowns.Clear();
            _playingByRuntimeId.Clear();
            _runtimeIdsBySoundId.Clear();
        }
    }
}
