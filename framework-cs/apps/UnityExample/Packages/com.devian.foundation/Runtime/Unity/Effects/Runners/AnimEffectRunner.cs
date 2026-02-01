using UnityEngine;

namespace Devian
{
    [RequireComponent(typeof(AnimSequencePlayer))]
    public sealed class AnimEffectRunner : MonoBehaviour, IEffectRunner
    {
        [Tooltip("Global speed multiplier forwarded to AnimSequencePlayer.PlayDefault().")]
        public float playSpeed = 1f;

        private EffectObject _owner;
        private AnimSequencePlayer _player;

        private float _computedPlayTime;

        /// <summary>
        /// -1 = infinite, 0 = no clip/unknown, >0 = seconds
        /// </summary>
        public float ComputedPlayTime => _computedPlayTime;

        public void _OnEffectAwake(EffectObject owner)
        {
            _owner = owner;
            _player = GetComponent<AnimSequencePlayer>(); // RequireComponent로 보장
        }

        public void _OnEffectPlay()
        {
            // default sequence 기준 playTime 계산
            _computedPlayTime = _player._GetDefaultPlayTime(playSpeed);

            // default sequence 재생
            _player.PlayDefault(playSpeed, _OnSequenceComplete);
        }

        public void _OnEffectPause()
        {
            _player.Pause(true);
        }

        public void _OnEffectResume()
        {
            _player.Pause(false);
        }

        public void _OnEffectStop()
        {
            _player.Stop(invokeCallback: false);
            _owner.Remove();
        }

        public void _OnEffectLateUpdate()
        {
            // 자동 타이머 제거 없음 (무한 이펙트 허용)
        }

        public void _OnEffectClear()
        {
            _computedPlayTime = 0f;
            _player.Stop(invokeCallback: false);
        }

        public void _SetSortingOrder(int order)
        {
            // Sprite sorting already handled by EffectObject.SetSortingOrder
        }

        private void _OnSequenceComplete()
        {
            // 무한이면 자동 제거 금지
            if (_computedPlayTime < 0f)
                return;

            _owner.Remove();
        }
    }
}
