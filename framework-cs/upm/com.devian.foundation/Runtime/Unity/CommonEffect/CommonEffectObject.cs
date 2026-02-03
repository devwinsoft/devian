using System;
using System.Collections.Generic;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Root common effect object (pool unit).
    /// Contains players (components implementing ICommonEffectPlayer).
    /// SSOT: skills/devian-unity/30-unity-components/22-common-effect-manager/SKILL.md
    /// </summary>
    public sealed class CommonEffectObject : MonoBehaviour, IPoolable<CommonEffectObject>
    {
        public event Action<CommonEffectObject> OnRemove;

        private enum State
        {
            Init,
            Playing,
            Paused,
            Stopped,
            Removed,
        }

        private State _state = State.Init;
        private Vector3 _initScale = Vector3.one;

        private readonly List<ICommonEffectPlayer> _players = new List<ICommonEffectPlayer>(8);

        private void Awake()
        {
            _initScale = transform.localScale;

            // Cache players (MonoBehaviours that implement ICommonEffectPlayer)
            _players.Clear();
            var list = GetComponentsInChildren<MonoBehaviour>(true);
            for (var i = 0; i < list.Length; i++)
            {
                var mb = list[i];
                if (mb == null) continue;

                if (mb is ICommonEffectPlayer player)
                {
                    _players.Add(player);
                }
            }

            for (var i = 0; i < _players.Count; i++)
            {
                _players[i]._OnEffectAwake(this);
            }
        }

        private void LateUpdate()
        {
            if (_state == State.Paused || _state == State.Removed)
            {
                return;
            }

            for (var i = 0; i < _players.Count; i++)
            {
                _players[i]._OnEffectLateUpdate();
            }
        }

        // ============================================================
        // Public API (effect lifecycle)
        // ============================================================

        public void Clear()
        {
            OnRemove = null;
            transform.localScale = _initScale;

            for (var i = 0; i < _players.Count; i++)
            {
                _players[i]._OnEffectClear();
            }

            _state = State.Init;
        }

        public void Init()
        {
            _state = State.Playing;
            for (var i = 0; i < _players.Count; i++)
            {
                _players[i]._OnEffectPlay();
            }
        }

        public void Play()
        {
            if (_state == State.Playing) return;
            _state = State.Playing;

            for (var i = 0; i < _players.Count; i++)
            {
                _players[i]._OnEffectPlay();
            }
        }

        public void Pause()
        {
            if (_state == State.Paused) return;
            _state = State.Paused;

            for (var i = 0; i < _players.Count; i++)
            {
                _players[i]._OnEffectPause();
            }
        }

        public void Resume()
        {
            if (_state != State.Paused) return;
            _state = State.Playing;

            for (var i = 0; i < _players.Count; i++)
            {
                _players[i]._OnEffectResume();
            }
        }

        public void Stop()
        {
            if (_state == State.Stopped || _state == State.Removed) return;
            _state = State.Stopped;

            for (var i = 0; i < _players.Count; i++)
            {
                _players[i]._OnEffectStop();
            }
        }

        public void Remove()
        {
            if (_state == State.Removed) return;
            _state = State.Removed;

            OnRemove?.Invoke(this);

            // Return to pool
            BundlePool.Despawn(this);
        }

        public void SetSortingOrder(int order)
        {
            // Default: apply to all SpriteRenderers
            var sprites = GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < sprites.Length; i++)
            {
                sprites[i].sortingOrder = order;
            }

            for (var i = 0; i < _players.Count; i++)
            {
                _players[i]._SetSortingOrder(order);
            }
        }

        public void SetDirection(bool reversed)
        {
            if (reversed)
            {
                transform.localScale = new Vector3(-_initScale.x, _initScale.y, _initScale.z);
            }
            else
            {
                transform.localScale = _initScale;
            }
        }

        // ============================================================
        // Pool Hooks
        // ============================================================

        public void OnPoolSpawned()
        {
            // Reset state on spawn
            _state = State.Init;
        }

        public void OnPoolDespawned()
        {
            Clear();
        }
    }
}
