using System;
using UnityEngine;

namespace Devian
{
    [RequireComponent(typeof(Animator))]
    public sealed class AnimSequencePlayer : MonoBehaviour
    {
        public event Action OnComplete;

        public bool IsPlaying => _isPlaying;
        public bool IsPaused => _isPaused;

        public float PlaySpeed
        {
            get => _playSpeed;
            set
            {
                _playSpeed = Mathf.Max(0f, value);
                _ApplyAnimatorSpeed();
            }
        }

        [SerializeField] private Animator _animator;

        // 편의: 컴포넌트에 기본 시퀀스를 박아 넣고 사용 가능
        [SerializeField] private AnimSequenceData _defaultSequence;

        private AnimSequenceStep[] _steps;
        private int _stepIndex;
        private int _repeatStartLoop; // 현재 스텝이 시작된 시점의 loop floor
        private bool _enteredTargetState;

        private string _targetStateName;

        private bool _isPlaying;
        private bool _isPaused;

        private float _playSpeed = 1f;
        private Action _callback;

        private const float Epsilon = 0.0001f;

        private void Awake()
        {
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
                if (_animator == null)
                {
                    _animator = GetComponent<Animator>();
                }
            }
        }

        public void PlayDefault(float playSpeed = 1f, Action onComplete = null, int startIndex = 0)
        {
            Play(_defaultSequence, playSpeed, onComplete, startIndex);
        }

        /// <summary>
        /// default sequence 기준 재생 시간 계산.
        /// -1 = 무한(Repeat==Loop 포함), 0 = clip 없음/계산불가, >0 = 초
        /// </summary>
        public float _GetDefaultPlayTime(float playSpeed = 1f)
        {
            return _ComputeSequencePlayTime(_defaultSequence, playSpeed);
        }

        private static float _ComputeSequencePlayTime(AnimSequenceData sequence, float playSpeed)
        {
            // playSpeed==0이면 진행 불가 → 계산 불가(0)
            if (Mathf.Abs(playSpeed) < Epsilon)
                return 0f;

            // 시퀀스/스텝/클립이 없으면 "clip이 없다"로 간주 → 0
            if (sequence == null || !sequence.IsValid() || sequence.Steps == null)
                return 0f;

            // 무한 체크: Repeat==Loop 스텝이 하나라도 있으면 -1
            for (int i = 0; i < sequence.Steps.Length; i++)
            {
                var step = sequence.Steps[i];
                if (step == null) continue;
                if (step.Repeat == AnimPlayCount.Loop)
                    return -1f;
            }

            float total = 0f;

            for (int i = 0; i < sequence.Steps.Length; i++)
            {
                var step = sequence.Steps[i];
                if (step == null) continue;

                var clip = step.Clip;
                if (clip == null)
                    return 0f; // clip이 없으면 0

                var stepSpeed = Mathf.Abs(step.Speed);
                if (stepSpeed < Epsilon)
                    return 0f;

                var repeatCount = (int)step.Repeat;
                if (repeatCount <= 0) repeatCount = 1;

                // clip 있으면 계산
                var per = clip.length / (playSpeed * stepSpeed);
                total += per * repeatCount;
            }

            return total > 0f ? total : 0f;
        }

        public void Play(AnimSequenceData sequence, float playSpeed = 1f, Action onComplete = null, int startIndex = 0)
        {
            if (_animator == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (sequence == null || !sequence.IsValid())
            {
                onComplete?.Invoke();
                return;
            }

            Stop(invokeCallback: false);

            _steps = sequence.Steps;
            _stepIndex = Mathf.Clamp(startIndex, 0, _steps.Length - 1);

            _WarnIfRepeatRequiresLooping(_steps);

            _playSpeed = Mathf.Max(0f, playSpeed);
            _callback = onComplete;

            _isPlaying = true;
            _isPaused = false;

            _PlayStep(_stepIndex, immediate: true);
        }

        public void Stop(bool invokeCallback)
        {
            if (invokeCallback && _callback != null)
            {
                var cb = _callback;
                _callback = null;
                cb.Invoke();
            }

            _isPlaying = false;
            _isPaused = false;

            _steps = null;
            _targetStateName = null;
            _enteredTargetState = false;
            _repeatStartLoop = 0;

            // Stop 시 animator speed는 기본으로 복귀
            if (_animator != null)
            {
                _animator.speed = 1f;
            }
        }

        public void Pause(bool paused)
        {
            _isPaused = paused;

            if (_animator == null) return;

            if (paused)
            {
                _animator.speed = 0f;
            }
            else
            {
                _ApplyAnimatorSpeed();
            }
        }

        public bool PlayNext()
        {
            if (!_isPlaying) return false;
            if (_steps == null) return false;
            if (_stepIndex < 0 || _stepIndex >= _steps.Length) return false;

            // 다음 스텝이 있으면 진행
            if (_stepIndex + 1 < _steps.Length)
            {
                _stepIndex++;
                _PlayStep(_stepIndex, immediate: false);
                return true;
            }

            // 다음 스텝이 없으면 시퀀스 종료
            _Complete();
            return false;
        }

        private void Update()
        {
            if (!_isPlaying || _isPaused) return;
            if (_animator == null) return;
            if (_steps == null || _stepIndex < 0 || _stepIndex >= _steps.Length) return;

            var step = _steps[_stepIndex];
            if (step == null || step.Clip == null)
            {
                Stop(invokeCallback: true);
                return;
            }

            // Loop 스텝은 자동 진행 없음
            if (step.Repeat == AnimPlayCount.Loop)
            {
                return;
            }

            // 목표 state 진입 확인(크로스페이드/플레이 직후에는 아직 진입 전일 수 있음)
            if (!_enteredTargetState)
            {
                var info = _animator.GetCurrentAnimatorStateInfo(0);
                if (!info.IsName(_targetStateName))
                {
                    return;
                }

                _enteredTargetState = true;
                _repeatStartLoop = Mathf.FloorToInt(info.normalizedTime);
                return;
            }

            // 현재 state가 목표 state인지 확인 (다른 애니가 끼어들면 여기서 안전하게 대기)
            {
                var info = _animator.GetCurrentAnimatorStateInfo(0);
                if (!info.IsName(_targetStateName))
                {
                    _enteredTargetState = false;
                    return;
                }

                // normalizedTime은 루프마다 1씩 증가
                var loops = Mathf.FloorToInt(info.normalizedTime) - _repeatStartLoop;

                var repeatCount = (int)step.Repeat;
                if (repeatCount <= 0) repeatCount = 1;

                if (loops < repeatCount)
                {
                    return;
                }
            }

            // 다음 스텝 진행
            if (_stepIndex + 1 < _steps.Length)
            {
                _stepIndex++;
                _PlayStep(_stepIndex, immediate: false);
                return;
            }

            _Complete();
        }

        private void _Complete()
        {
            _isPlaying = false;

            if (_callback != null)
            {
                var cb = _callback;
                _callback = null;
                cb.Invoke();
            }

            OnComplete?.Invoke();
        }

        private void _WarnIfRepeatRequiresLooping(AnimSequenceStep[] steps)
        {
            if (steps == null) return;

            for (var i = 0; i < steps.Length; i++)
            {
                var step = steps[i];
                if (step == null) continue;

                var clip = step.Clip;
                if (clip == null) continue;

                // Repeat가 One이 아니면(Loop 포함) 루프 클립이어야 함
                if (step.Repeat != AnimPlayCount.One && !clip.isLooping)
                {
                    Debug.LogWarning(
                        $"[AnimSequencePlayer] Step #{i} repeats ({step.Repeat}) but clip is not looping. " +
                        $"This may block progression. Clip='{clip.name}'. " +
                        $"Fix: enable Loop Time on the clip import settings or set Repeat=One.",
                        this);
                }
            }
        }

        private void _PlayStep(int index, bool immediate)
        {
            var step = _steps[index];
            if (step == null || step.Clip == null)
            {
                Stop(invokeCallback: true);
                return;
            }

            _targetStateName = step.Clip.name; // 규약: stateName == clip.name
            _enteredTargetState = false;
            _repeatStartLoop = 0;

            _ApplyAnimatorSpeed();

            var fade = Mathf.Max(0f, step.FadeTime);
            if (immediate || fade <= Epsilon)
            {
                _animator.Play(_targetStateName, 0, 0f);
            }
            else
            {
                _animator.CrossFade(_targetStateName, fade, 0, 0f);
            }
        }

        private void _ApplyAnimatorSpeed()
        {
            if (_animator == null) return;
            if (_isPaused)
            {
                _animator.speed = 0f;
                return;
            }

            if (_steps == null || _stepIndex < 0 || _stepIndex >= _steps.Length)
            {
                _animator.speed = Mathf.Max(Epsilon, _playSpeed);
                return;
            }

            var step = _steps[_stepIndex];
            var stepSpeed = step != null ? step.Speed : 1f;
            var speed = Mathf.Max(Epsilon, _playSpeed) * Mathf.Max(Epsilon, stepSpeed);
            _animator.speed = speed;
        }
    }
}
