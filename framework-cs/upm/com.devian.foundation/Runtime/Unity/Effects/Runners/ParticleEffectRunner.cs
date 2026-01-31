using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Particle-based effect runner.
    /// playTime > 0: stop after time
    /// playTime == 0: stop when all particles are dead
    /// stop: StopEmitting then wait fadeOutTime then owner.Remove()
    /// </summary>
    public sealed class ParticleEffectRunner : MonoBehaviour, IEffectRunner
    {
        public float playTime = 0f;
        public float fadeOutTime = 0f;

        private EffectObject _owner;
        private ParticleSystem[] _particles;

        private float _remainPlay;
        private bool _stopping;
        private float _remainFade;

        public void _OnEffectAwake(EffectObject owner)
        {
            _owner = owner;
            _particles = GetComponentsInChildren<ParticleSystem>(true);
        }

        public void _OnEffectPlay()
        {
            _stopping = false;
            _remainFade = 0f;

            _remainPlay = playTime;

            if (_particles != null)
            {
                for (var i = 0; i < _particles.Length; i++)
                {
                    var ps = _particles[i];
                    if (ps == null) continue;
                    ps.Play(true);
                }
            }
        }

        public void _OnEffectPause()
        {
            if (_particles == null) return;

            for (var i = 0; i < _particles.Length; i++)
            {
                var ps = _particles[i];
                if (ps == null) continue;

                if (ps.isPlaying)
                {
                    ps.Pause(true);
                }
            }
        }

        public void _OnEffectResume()
        {
            if (_particles == null) return;

            for (var i = 0; i < _particles.Length; i++)
            {
                var ps = _particles[i];
                if (ps == null) continue;

                if (ps.isPaused)
                {
                    ps.Play(true);
                }
            }
        }

        public void _OnEffectStop()
        {
            if (_stopping) return;
            _stopping = true;

            if (_particles != null)
            {
                for (var i = 0; i < _particles.Length; i++)
                {
                    var ps = _particles[i];
                    if (ps == null) continue;
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }

            if (fadeOutTime > 0f)
            {
                _remainFade = fadeOutTime;
            }
            else
            {
                _owner.Remove();
            }
        }

        public void _OnEffectLateUpdate()
        {
            if (_owner == null) return;

            if (_stopping)
            {
                if (_remainFade > 0f)
                {
                    _remainFade -= Time.deltaTime;
                    if (_remainFade <= 0f)
                    {
                        _owner.Remove();
                    }
                }
                return;
            }

            if (playTime > 0f)
            {
                _remainPlay -= Time.deltaTime;
                if (_remainPlay <= 0f)
                {
                    _owner.Stop();
                }
            }
            else
            {
                // stop when all particles are dead
                var anyAlive = false;

                if (_particles != null)
                {
                    for (var i = 0; i < _particles.Length; i++)
                    {
                        var ps = _particles[i];
                        if (ps == null) continue;

                        if (ps.IsAlive(true))
                        {
                            anyAlive = true;
                            break;
                        }
                    }
                }

                if (!anyAlive)
                {
                    _owner.Stop();
                }
            }
        }

        public void _OnEffectClear()
        {
            _stopping = false;
            _remainFade = 0f;
            _remainPlay = 0f;

            if (_particles == null) return;

            for (var i = 0; i < _particles.Length; i++)
            {
                var ps = _particles[i];
                if (ps == null) continue;

                ps.Stop(true);
                ps.Clear(true);
            }
        }

        public void _SetSortingOrder(int order)
        {
            // Optional: ParticleSystemRenderer sorting order if needed
            var renderers = GetComponentsInChildren<ParticleSystemRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                renderers[i].sortingOrder = order;
            }
        }
    }
}
