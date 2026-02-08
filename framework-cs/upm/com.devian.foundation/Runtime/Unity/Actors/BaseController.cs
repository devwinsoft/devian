using UnityEngine;

namespace Devian
{
    public abstract class BaseController : MonoBehaviour
    {
        private BaseActor _actor;
        private bool _initialized;
        private bool _cleared;

        public virtual int Priority => 0;

        protected virtual void Awake()
        {
            onAwake();
        }

        protected virtual void onAwake() { }

        /// <summary>
        /// Called by BaseActor.RegisterController&lt;T&gt;() when added to actor list.
        /// </summary>
        public void Init(BaseActor actor)
        {
            if (_initialized) return;
            if (_cleared) return;

            _actor = actor;
            _initialized = true;

            onInit(actor);
        }

        protected virtual void onInit(BaseActor actor) { }

        /// <summary>
        /// Called by actor.Clear() (also on pool despawn / destroy).
        /// </summary>
        public virtual void Clear()
        {
            if (_cleared) return;
            _cleared = true;

            onClear();

            _initialized = false;
            _actor = null;
        }

        protected virtual void onClear() { }

        public BaseActor Actor => _actor;
        public bool IsInitialized => _initialized;
        public bool IsCleared => _cleared;
    }
}
