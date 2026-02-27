using UnityEngine;

namespace Devian
{
    public abstract class ActorController<TOwner> : MonoBehaviour
    {
        private TOwner _owner;
        private bool _initialized;
        private bool _cleared;

        public virtual int Priority => 0;

        protected virtual void Awake()
        {
            onAwake();
        }

        protected virtual void onAwake() { }

        /// <summary>
        /// Called by owner Init loop or standalone Awake.
        /// </summary>
        public void Init(TOwner owner)
        {
            if (_initialized) return;
            if (_cleared) return;

            _owner = owner;
            _initialized = true;

            onInit(owner);
        }

        protected virtual void onInit(TOwner owner) { }

        /// <summary>
        /// Called by owner.Clear() (also on pool despawn / destroy).
        /// </summary>
        public virtual void Clear()
        {
            if (_cleared) return;
            _cleared = true;

            onClear();

            _initialized = false;
            _owner = default;
        }

        protected virtual void onClear() { }

        public TOwner Owner => _owner;
        public bool IsInitialized => _initialized;
        public bool IsCleared => _cleared;
    }
}
