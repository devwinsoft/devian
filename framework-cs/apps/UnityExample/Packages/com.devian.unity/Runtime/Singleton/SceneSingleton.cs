// Unity Singleton - SceneSingleton<T>
// SSOT: skills/devian-unity/30-unity-components/01-singleton/SKILL.md
// NOTE: 이 파일은 Generated 폴더 산출물이 아닌 고정 유틸(수기 유지)이며,
//       정본은 upm 경로다. Packages는 복사본.

using System;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Scene-placed singleton - no auto-creation.
    /// Instance must be placed in scene or explicitly registered.
    /// Accessing Instance without prior registration throws InvalidOperationException.
    /// </summary>
    /// <typeparam name="T">Concrete singleton type</typeparam>
    public abstract class SceneSingleton<T> : MonoBehaviour where T : SceneSingleton<T>
    {
        private static T s_instance;
        private static readonly object s_lock = new object();
        
        /// <summary>
        /// Returns true if an instance exists.
        /// </summary>
        public static bool HasInstance => s_instance != null;
        
        /// <summary>
        /// Gets the singleton instance.
        /// Throws InvalidOperationException if not registered (scene placement required).
        /// </summary>
        public static T Instance
        {
            get
            {
                if (s_instance == null)
                {
                    throw new InvalidOperationException(
                        $"[{typeof(T).Name}] Instance not found. " +
                        "SceneSingleton requires scene placement or explicit Register() call.");
                }
                return s_instance;
            }
        }
        
        /// <summary>
        /// Registers an instance explicitly. Main thread only.
        /// Does NOT create a new instance - use scene placement or manual instantiation.
        /// </summary>
        /// <param name="instance">Instance to register</param>
        public static void Register(T instance)
        {
            UnityMainThread.EnsureOrThrow($"{typeof(T).Name}.Register");
            
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));
            
            lock (s_lock)
            {
                if (s_instance != null && s_instance != instance)
                {
                    Debug.LogWarning($"[{typeof(T).Name}] Instance already registered. Destroying duplicate.");
                    UnityEngine.Object.Destroy(instance.gameObject);
                    return;
                }
                
                s_instance = instance;
                DontDestroyOnLoad(instance.gameObject);
            }
        }
        
        /// <summary>
        /// Called by Unity. Handles duplicate detection and registration.
        /// </summary>
        protected virtual void Awake()
        {
            lock (s_lock)
            {
                if (s_instance != null && s_instance != this)
                {
                    Debug.LogWarning($"[{typeof(T).Name}] Duplicate instance detected. Destroying this.");
                    UnityEngine.Object.Destroy(gameObject);
                    return;
                }
                
                s_instance = (T)this;
                DontDestroyOnLoad(gameObject);
            }
        }
        
        /// <summary>
        /// Called by Unity. Clears instance reference if this is the registered instance.
        /// </summary>
        protected virtual void OnDestroy()
        {
            lock (s_lock)
            {
                if (s_instance == this)
                {
                    s_instance = null;
                }
            }
        }
    }
}
