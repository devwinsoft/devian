using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Devian
{
    /// <summary>
    /// Ensures UI input infrastructure (EventSystem + InputSystemUIInputModule).
    /// Separate from UIManager to maintain SSOT for input responsibilities.
    /// Must be attached to Bootstrap prefab (CompoSingleton).
    /// </summary>
    public sealed class UIInputManager : CompoSingleton<UIInputManager>
    {
        private EventSystem _eventSystem;

        /// <summary>
        /// The managed EventSystem instance.
        /// </summary>
        public EventSystem EventSystem => _eventSystem;

        protected override void Awake()
        {
            base.Awake();
            ensureEventSystem();
            ensureInputModule();
        }

        /// <summary>
        /// Ensures an EventSystem exists in the scene.
        /// Creates one under Bootstrap if none found.
        /// Logs warning if multiple found (does not destroy duplicates).
        /// </summary>
        private void ensureEventSystem()
        {
            var eventSystems = FindObjectsOfType<EventSystem>(true);

            if (eventSystems == null || eventSystems.Length == 0)
            {
                // Create new EventSystem under Bootstrap
                var go = new GameObject("EventSystem");
                go.transform.SetParent(transform, false);
                _eventSystem = go.AddComponent<EventSystem>();
            }
            else
            {
                // Use first found
                _eventSystem = eventSystems[0];

                // Warn if multiple exist
                if (eventSystems.Length > 1)
                {
                    Debug.LogWarning(
                        $"[UIInputManager] Multiple EventSystems found ({eventSystems.Length}). " +
                        $"Using '{_eventSystem.name}'. Consider removing duplicates manually.");
                }
            }
        }

        /// <summary>
        /// Ensures InputSystemUIInputModule exists on EventSystem.
        /// Removes StandaloneInputModule if present.
        /// Uses reflection to avoid hard dependency on Input System package.
        /// </summary>
        private void ensureInputModule()
        {
            if (_eventSystem == null)
            {
                return;
            }

            // Remove StandaloneInputModule if present
            var standalone = _eventSystem.GetComponent<StandaloneInputModule>();
            if (standalone != null)
            {
                Destroy(standalone);
            }

            // Try to get InputSystemUIInputModule type via reflection
            var inputModuleType = Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");

            if (inputModuleType == null)
            {
                // Fallback without assembly qualifier
                inputModuleType = Type.GetType(
                    "UnityEngine.InputSystem.UI.InputSystemUIInputModule");
            }

            if (inputModuleType == null)
            {
                Debug.LogError(
                    "[UIInputManager] Input System UI module type not found. " +
                    "Ensure Unity Input System package is installed.");
                return;
            }

            // Check if component already exists
            var existingModule = _eventSystem.GetComponent(inputModuleType);
            if (existingModule == null)
            {
                _eventSystem.gameObject.AddComponent(inputModuleType);
            }
        }
    }
}
