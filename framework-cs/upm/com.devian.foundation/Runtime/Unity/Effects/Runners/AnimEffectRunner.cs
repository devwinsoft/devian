using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Animator-based effect runner.
    /// - If clip is set: remove after clip.length (considering animator speed)
    /// - Else if playTime > 0: remove after playTime
    /// - Else: try to read current state's length after Play (best-effort)
    /// </summary>
    public sealed class AnimEffectRunner : MonoBehaviour, IEffectRunner
    {
        public bool playOnStart = true;

        [Tooltip("Optional: explicit clip used for duration.")]
        public AnimationClip clip;

        [Tooltip("Optional: fallback duration if clip is not set.")]
        public float playTime = 0f;

        [Tooltip("Animator state name to play (optional). If empty, uses default state.")]
        public string stateName = "";

        [Tooltip("Animator layer index.")]
        public int layer = 0;

        private EffectObject _owner;
        private Animator _animator;

        private float _remain;
        private bool _needFetchStateLength;

        public void _OnEffectAwake(EffectObject owner)
        {
            _owner = owner;
            _animator = GetComponentInChildren<Animator>(true);

            if (playOnStart)
            {
                // Actual Play() is called by owner.Init(), not here.
            }
        }

        public void _OnEffectPlay()
        {
            _remain = 0f;
            _needFetchStateLength = false;

            if (_animator == null)
            {
                // No animator: if playTime is set, use it; else remove immediately.
                if (playTime > 0f)
                {
                    _remain = playTime;
                }
                else
                {
                    _owner.Remove();
                }
                return;
            }

            _animator.speed = 1f;

            if (!string.IsNullOrEmpty(stateName))
            {
                _animator.Play(stateName, layer, 0f);
            }

            if (clip != null)
            {
                var speed = Mathf.Abs(_animator.speed);
                if (speed < 0.0001f) speed = 1f;

                _remain = clip.length / speed;
                return;
            }

            if (playTime > 0f)
            {
                _remain = playTime;
                return;
            }

            // Best-effort: read state length next LateUpdate
            _needFetchStateLength = true;
        }

        public void _OnEffectPause()
        {
            if (_animator != null)
            {
                _animator.speed = 0f;
            }
        }

        public void _OnEffectResume()
        {
            if (_animator != null)
            {
                _animator.speed = 1f;
            }
        }

        public void _OnEffectStop()
        {
            _owner.Remove();
        }

        public void _OnEffectLateUpdate()
        {
            if (_owner == null) return;

            if (_needFetchStateLength && _animator != null)
            {
                var info = _animator.GetCurrentAnimatorStateInfo(layer);
                var len = info.length;
                if (len > 0.0001f)
                {
                    var speed = Mathf.Abs(_animator.speed);
                    if (speed < 0.0001f) speed = 1f;

                    _remain = len / speed;
                    _needFetchStateLength = false;
                }
                else
                {
                    // If still unavailable, fallback to 1 sec and stop trying
                    _remain = 1f;
                    _needFetchStateLength = false;
                }
            }

            if (_remain > 0f)
            {
                _remain -= Time.deltaTime;
                if (_remain <= 0f)
                {
                    _owner.Remove();
                }
            }
        }

        public void _OnEffectClear()
        {
            _remain = 0f;
            _needFetchStateLength = false;

            if (_animator != null)
            {
                _animator.speed = 1f;
                _animator.Rebind();
                _animator.Update(0f);
            }
        }

        public void _SetSortingOrder(int order)
        {
            // Sprite sorting already handled by EffectObject.SetSortingOrder
        }
    }
}
