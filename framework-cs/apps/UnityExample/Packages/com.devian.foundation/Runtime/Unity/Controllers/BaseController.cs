using UnityEngine;

namespace Devian
{
    /// <summary>
    /// 컨트롤러 공통 베이스.
    /// 현재 단계에서는 Owner 바인딩만 제공.
    /// SSOT: skills/devian-unity/30-unity-components/30-base-controller/SKILL.md
    /// </summary>
    public abstract class BaseController<TOwner> : MonoBehaviour
    {
        private TOwner _owner;
        private bool _isInitialized;

        public TOwner Owner => _owner;
        public bool IsInitialized => _isInitialized;

        public virtual void Clear()
        {
            _owner = default;
            _isInitialized = false;
        }

        /// <summary>
        /// Owner 1회 바인딩 + OnInit 호출.
        /// </summary>
        public void Init(TOwner owner)
        {
            if (_isInitialized)
            {
                Debug.LogWarning($"[{GetType().Name}] Already initialized. Init call ignored.");
                return;
            }

            _owner = owner;
            _isInitialized = true;

            OnInit();
        }

        /// <summary>
        /// Init 완료 후 확장 훅.
        /// </summary>
        protected virtual void OnInit()
        {
        }
    }
}
