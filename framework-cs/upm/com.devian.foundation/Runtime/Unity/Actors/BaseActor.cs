using System.Collections.Generic;
using UnityEngine;

namespace Devian
{
    public abstract class BaseActor : MonoBehaviour, IPoolable<BaseActor>
    {
        private readonly List<BaseController> _controllers = new();
        private bool _initialized;
        private bool _cleared;

        protected virtual void Awake()
        {
            onAwake();
        }

        /// <summary>
        /// Called from Awake() only. Do NOT call Init() here.
        /// </summary>
        protected virtual void onAwake() { }

        /// <summary>
        /// Must be called from outside after essential components are initialized.
        /// </summary>
        public void Init()
        {
            if (_initialized) return;
            if (_cleared) return;

            _initialized = true;
            onInit();

            for (int i = 0; i < _controllers.Count; i++)
            {
                var c = _controllers[i];
                if (c == null) continue;
                c.Init(this);
            }

            onPostInit();
        }

        protected virtual void onInit() { }
        protected virtual void onPostInit() { }

        /// <summary>
        /// External call preferred. Also called from OnDestroy() / OnPoolDespawned() as defense.
        /// Must be virtual by requirement.
        /// </summary>
        public virtual void Clear()
        {
            if (_cleared) return;
            _cleared = true;

            onClear();

            for (int i = _controllers.Count - 1; i >= 0; i--)
            {
                var c = _controllers[i];
                if (c == null) continue;
                c.Clear();
            }

            _controllers.Clear();
            _initialized = false;

            onPostClear();
        }

        protected virtual void onClear() { }
        protected virtual void onPostClear() { }

        protected virtual void OnDestroy()
        {
            Clear();
        }

        // --- Pool hooks (SSOT: 02-pool-manager / 04-pool-factories) ---

        public virtual void OnPoolSpawned()
        {
            _cleared = false;
            _initialized = false;
        }

        public virtual void OnPoolDespawned()
        {
            Clear();
        }

        // --- Controller registry ---

        public T RegisterController<T>() where T : BaseController
        {
            T controller = gameObject.GetComponent<T>();
            if (controller == null)
                controller = gameObject.AddComponent<T>();

            if (!_controllers.Contains(controller))
                _controllers.Add(controller);

            return controller;
        }

        public T RegisterController<T>(GameObject obj) where T : BaseController
        {
            T controller = obj.GetComponent<T>();
            if (controller == null)
                controller = obj.AddComponent<T>();

            if (!_controllers.Contains(controller))
                _controllers.Add(controller);

            return controller;
        }

        public bool UnregisterController(BaseController controller)
        {
            if (controller == null) return false;
            return _controllers.Remove(controller);
        }

        public IReadOnlyList<BaseController> Controllers => _controllers;
        public bool IsInitialized => _initialized;
        public bool IsCleared => _cleared;
    }
}
