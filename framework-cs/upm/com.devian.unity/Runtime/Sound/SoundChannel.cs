// SSOT: skills/devian-unity/30-unity-components/17-sound-manager/SKILL.md

#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// 사운드 채널. 풀/재생 리스트/쿨타임/SEQ를 관리한다.
    /// SoundManager는 AudioSource를 직접 들지 않고 SoundChannel에 위임한다.
    /// </summary>
    public sealed class SoundChannel : MonoBehaviour
    {
        private SoundChannelType _channelType;
        private readonly List<SoundPlay> _pool = new();
        private readonly List<SoundPlay> _activePlays = new();
        private readonly Dictionary<string, float> _cooldowns = new();

        private float _volume = 1f;
        private bool _isMuted;
        private int _poolSize = 4;

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
                CreatePooledPlay();
            }
        }

        private SoundPlay CreatePooledPlay()
        {
            var go = new GameObject($"SoundPlay_{_channelType}_{_pool.Count}");
            go.transform.SetParent(transform);
            var play = go.AddComponent<SoundPlay>();
            go.SetActive(false);
            _pool.Add(play);
            return play;
        }

        private SoundPlay GetFromPool()
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
            return CreatePooledPlay();
        }

        /// <summary>
        /// 사운드를 재생한다.
        /// </summary>
        /// <returns>재생 중인 SoundPlay 인스턴스. 쿨타임 등으로 재생되지 않으면 null.</returns>
        public SoundPlay? Play(
            string soundId,
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
            if (cooltime > 0f && IsOnCooldown(soundId))
            {
                return null;
            }

            var play = GetFromPool();
            play.gameObject.SetActive(true);

            var effectiveVolume = _isMuted ? 0f : volume * _volume;
            play.Play(clip, effectiveVolume, loop, fadeInSeconds, fadeOutSeconds, waitSeconds, groupId, pitch, is3d, position, areaClose, areaFar);

            _activePlays.Add(play);

            // 쿨다운 등록
            if (cooltime > 0f)
            {
                _cooldowns[soundId] = Time.realtimeSinceStartup + cooltime;
            }

            return play;
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
            UpdateActiveVolumes();
        }

        /// <summary>
        /// 음소거 상태를 설정한다.
        /// </summary>
        public void SetMute(bool mute)
        {
            _isMuted = mute;
            UpdateActiveVolumes();
        }

        private void UpdateActiveVolumes()
        {
            foreach (var play in _activePlays)
            {
                if (play.AudioSource != null)
                {
                    play.AudioSource.mute = _isMuted;
                }
            }
        }

        private bool IsOnCooldown(string soundId)
        {
            if (_cooldowns.TryGetValue(soundId, out var cooldownEnd))
            {
                return Time.realtimeSinceStartup < cooldownEnd;
            }
            return false;
        }

        private void Update()
        {
            // 완료된 재생을 풀로 반환
            for (int i = _activePlays.Count - 1; i >= 0; i--)
            {
                var play = _activePlays[i];
                if (play.CurrentState == SoundPlay.State.Finished)
                {
                    play.Reset();
                    play.gameObject.SetActive(false);
                    _activePlays.RemoveAt(i);
                }
            }

            // 만료된 쿨다운 정리 (가끔씩)
            if (Time.frameCount % 300 == 0)
            {
                CleanupExpiredCooldowns();
            }
        }

        private void CleanupExpiredCooldowns()
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
        }
    }
}
