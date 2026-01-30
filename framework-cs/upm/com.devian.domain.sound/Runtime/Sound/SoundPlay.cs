// SSOT: skills/devian-unity/30-unity-components/17-sound-manager/SKILL.md

#nullable enable

using UnityEngine;

namespace Devian
{
    /// <summary>
    /// 개별 사운드 재생 유닛. Wait/FadeIn/Play/FadeOut 상태를 처리한다.
    /// SoundChannel이 풀링하여 재사용한다.
    /// generation을 통해 풀 재사용 시 잘못된 제어를 방지한다.
    /// </summary>
    public sealed class SoundPlay : MonoBehaviour
    {
        public enum State
        {
            Idle,
            Wait,
            FadeIn,
            Playing,
            FadeOut,
            Paused,
            Finished
        }

        private AudioSource? _audioSource;
        private State _state = State.Idle;
        private float _targetVolume = 1f;
        private float _currentVolume = 1f;
        private float _fadeTimer;
        private float _fadeInDuration;
        private float _fadeOutDuration;
        private float _waitTime;
        private int _groupId;
        private float _pitch = 1f;

        // generation: 풀에서 재사용될 때마다 증가 (잘못된 제어 방지)
        private int _generation;

        // runtime tracking
        private SoundRuntimeId _runtimeId;
        private string _soundId = string.Empty;
        private int _rowId;
        private float _startTime;

        // Pause 상태 저장
        private State _stateBeforePause = State.Idle;
        private float _pausedTime;

        public State CurrentState => _state;
        public AudioSource? AudioSource => _audioSource;
        public int GroupId => _groupId;
        public int Generation => _generation;
        public SoundRuntimeId RuntimeId => _runtimeId;
        public string SoundId => _soundId;
        public int RowId => _rowId;
        public float StartTime => _startTime;
        public float TargetVolume => _targetVolume;
        public float Pitch => _pitch;
        public bool Loop => _audioSource?.loop ?? false;

        public bool IsPlaying => _state == State.Playing || _state == State.FadeIn;
        public bool IsPaused => _state == State.Paused;
        public bool IsActive => _state != State.Idle && _state != State.Finished;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
            _audioSource.playOnAwake = false;
        }

        /// <summary>
        /// 풀에서 Acquire 시 generation을 증가시키고 runtime 정보를 설정한다.
        /// </summary>
        public void Acquire(SoundRuntimeId runtimeId, string soundId, int rowId)
        {
            _generation++;
            _runtimeId = runtimeId;
            _soundId = soundId;
            _rowId = rowId;
            _startTime = Time.realtimeSinceStartup;
        }

        /// <summary>
        /// 현재 generation과 일치하는지 확인한다.
        /// 불일치하면 이미 풀에서 재사용된 것이므로 명령을 무시해야 한다.
        /// </summary>
        public bool ValidateGeneration(int expectedGeneration)
        {
            return _generation == expectedGeneration;
        }

        /// <summary>
        /// 사운드 재생을 시작한다.
        /// </summary>
        public void Play(
            AudioClip clip,
            float volume = 1f,
            bool loop = false,
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
            if (_audioSource == null) return;

            _audioSource.clip = clip;
            _audioSource.loop = loop;
            _audioSource.pitch = pitch;
            _targetVolume = volume;
            _currentVolume = volume;
            _groupId = groupId;
            _pitch = pitch;
            _fadeInDuration = fadeInSeconds;
            _fadeOutDuration = fadeOutSeconds;
            _waitTime = waitSeconds;

            // 3D 설정
            if (is3d && position.HasValue)
            {
                _audioSource.spatialBlend = 1f;
                _audioSource.minDistance = areaClose;
                _audioSource.maxDistance = areaFar;
                transform.position = position.Value;
            }
            else
            {
                _audioSource.spatialBlend = 0f;
            }

            // 상태 전이
            if (waitSeconds > 0f)
            {
                _state = State.Wait;
                _fadeTimer = 0f;
            }
            else if (fadeInSeconds > 0f)
            {
                _state = State.FadeIn;
                _fadeTimer = 0f;
                _audioSource.volume = 0f;
                _audioSource.Play();
            }
            else
            {
                _state = State.Playing;
                _audioSource.volume = volume;
                _audioSource.Play();
            }
        }

        /// <summary>
        /// 재생을 즉시 정지한다.
        /// </summary>
        public void Stop()
        {
            if (_audioSource != null)
            {
                _audioSource.Stop();
                _audioSource.clip = null;
            }
            _state = State.Finished;
        }

        /// <summary>
        /// 재생을 일시정지한다.
        /// </summary>
        public bool Pause()
        {
            if (_state != State.Playing && _state != State.FadeIn && _state != State.Wait)
            {
                return false;
            }

            _stateBeforePause = _state;
            _pausedTime = _audioSource?.time ?? 0f;

            if (_audioSource != null)
            {
                _audioSource.Pause();
            }

            _state = State.Paused;
            return true;
        }

        /// <summary>
        /// 일시정지를 해제한다.
        /// </summary>
        public bool Resume()
        {
            if (_state != State.Paused)
            {
                return false;
            }

            if (_audioSource != null)
            {
                _audioSource.UnPause();
            }

            _state = _stateBeforePause;
            return true;
        }

        /// <summary>
        /// 볼륨을 설정한다.
        /// </summary>
        public void SetVolume(float volume)
        {
            _targetVolume = volume;
            _currentVolume = volume;
            if (_audioSource != null && _state != State.FadeIn && _state != State.FadeOut)
            {
                _audioSource.volume = volume;
            }
        }

        /// <summary>
        /// 피치를 설정한다.
        /// </summary>
        public void SetPitch(float pitch)
        {
            _pitch = pitch;
            if (_audioSource != null)
            {
                _audioSource.pitch = pitch;
            }
        }

        /// <summary>
        /// 3D 위치를 설정한다.
        /// </summary>
        public void SetPosition(Vector3 position)
        {
            transform.position = position;
        }

        /// <summary>
        /// 페이드 아웃 후 정지한다.
        /// </summary>
        public void FadeOutAndStop(float fadeOutSeconds)
        {
            if (_state == State.Idle || _state == State.Finished) return;

            _fadeOutDuration = fadeOutSeconds;
            _fadeTimer = 0f;
            _state = State.FadeOut;
        }

        /// <summary>
        /// 풀에 반환하기 전 상태를 초기화한다.
        /// generation은 유지한다 (다음 Acquire에서 증가).
        /// </summary>
        public void Reset()
        {
            Stop();
            _state = State.Idle;
            _targetVolume = 1f;
            _currentVolume = 1f;
            _fadeTimer = 0f;
            _fadeInDuration = 0f;
            _fadeOutDuration = 0f;
            _waitTime = 0f;
            _groupId = 0;
            _pitch = 1f;
            _runtimeId = SoundRuntimeId.Invalid;
            _soundId = string.Empty;
            _rowId = 0;
            _startTime = 0f;
            _stateBeforePause = State.Idle;
            _pausedTime = 0f;
            // _generation은 유지
        }

        private void Update()
        {
            if (_audioSource == null) return;

            switch (_state)
            {
                case State.Wait:
                    _fadeTimer += Time.deltaTime;
                    if (_fadeTimer >= _waitTime)
                    {
                        _fadeTimer = 0f;
                        if (_fadeInDuration > 0f)
                        {
                            _state = State.FadeIn;
                            _audioSource.volume = 0f;
                            _audioSource.Play();
                        }
                        else
                        {
                            _state = State.Playing;
                            _audioSource.volume = _targetVolume;
                            _audioSource.Play();
                        }
                    }
                    break;

                case State.FadeIn:
                    _fadeTimer += Time.deltaTime;
                    var fadeInT = Mathf.Clamp01(_fadeTimer / _fadeInDuration);
                    _audioSource.volume = fadeInT * _targetVolume;
                    if (fadeInT >= 1f)
                    {
                        _state = State.Playing;
                    }
                    break;

                case State.Playing:
                    // 루프가 아니면 재생 완료 체크
                    if (!_audioSource.loop && !_audioSource.isPlaying)
                    {
                        _state = State.Finished;
                    }
                    break;

                case State.FadeOut:
                    _fadeTimer += Time.deltaTime;
                    var fadeOutT = Mathf.Clamp01(_fadeTimer / _fadeOutDuration);
                    _audioSource.volume = (1f - fadeOutT) * _currentVolume;
                    if (fadeOutT >= 1f)
                    {
                        Stop();
                    }
                    break;

                case State.Paused:
                    // 일시정지 상태에서는 아무것도 하지 않음
                    break;
            }
        }
    }
}
