using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Central manager for UI Canvas lifecycle.
    /// Provides Canvas lookup, creation, utility methods.
    /// AutoSingleton: auto-created on first Instance access.
    /// </summary>
    public sealed class UIManager : AutoSingleton<UIManager>
    {
        private UIMessageSystem mMessageSystem = new UIMessageSystem();

        /// <summary>
        /// UI message system for UI-level messaging (ReloadText, Resize, etc.).
        /// </summary>
        public static UIMessageSystem messageSystem => Instance.mMessageSystem;

        /// <summary>
        /// Tries to get an existing canvas of the specified type.
        /// First checks Singleton registry, then searches scene.
        /// </summary>
        /// <typeparam name="TCanvas">The canvas type.</typeparam>
        /// <param name="canvas">The found canvas, or null.</param>
        /// <returns>True if canvas was found.</returns>
        public bool TryGetCanvas<TCanvas>(out TCanvas canvas)
            where TCanvas : MonoBehaviour
        {
            // 1. Try Singleton registry first
            if (Singleton.TryGet<TCanvas>(out canvas))
            {
                return true;
            }

            // 2. Search scene (including inactive)
            canvas = FindObjectOfType<TCanvas>(true);
            return canvas != null;
        }

        /// <summary>
        /// Creates a new canvas from a prefab using BundlePool.
        /// If a singleton canvas of the same type already exists,
        /// despawns the new instance and returns the existing one.
        /// </summary>
        /// <typeparam name="TCanvas">The canvas type.</typeparam>
        /// <param name="prefabName">The prefab asset name.</param>
        /// <param name="parent">Optional parent transform.</param>
        /// <returns>The canvas instance (existing or newly created).</returns>
        public TCanvas CreateCanvas<TCanvas>(string prefabName, Transform parent = null)
            where TCanvas : MonoBehaviour, IPoolable<TCanvas>
        {
            var spawned = BundlePool.Spawn<TCanvas>(prefabName, parent: parent);

            // Duplicate check: if existing singleton exists and is different from spawned
            if (Singleton.TryGet<TCanvas>(out var existing) && existing != spawned)
            {
                // Despawn the duplicate and return existing
                BundlePool.Despawn(spawned);
                return existing;
            }

            return spawned;
        }

        /// <summary>
        /// Ensures a canvas exists, creating it if necessary.
        /// </summary>
        /// <typeparam name="TCanvas">The canvas type.</typeparam>
        /// <param name="prefabName">The prefab asset name (used only if creation needed).</param>
        /// <param name="parent">Optional parent transform (used only if creation needed).</param>
        /// <returns>The canvas instance.</returns>
        public TCanvas EnsureCanvas<TCanvas>(string prefabName, Transform parent = null)
            where TCanvas : MonoBehaviour, IPoolable<TCanvas>
        {
            if (TryGetCanvas<TCanvas>(out var canvas))
            {
                return canvas;
            }

            return CreateCanvas<TCanvas>(prefabName, parent);
        }

        /// <summary>
        /// Despawns a canvas back to its pool.
        /// Note: Only use for poolable canvases. Non-poolable canvases should be destroyed directly.
        /// </summary>
        /// <typeparam name="TCanvas">The canvas type.</typeparam>
        public void DespawnCanvas<TCanvas>()
            where TCanvas : MonoBehaviour
        {
            if (TryGetCanvas<TCanvas>(out var canvas))
            {
                BundlePool.Despawn(canvas);
            }
        }

        /// <summary>
        /// Validates a canvas configuration.
        /// </summary>
        /// <typeparam name="TCanvas">The canvas type (must be UICanvas).</typeparam>
        /// <param name="reason">Output reason if validation fails.</param>
        /// <returns>True if valid.</returns>
        public bool ValidateCanvas<TCanvas>(out string reason)
            where TCanvas : UICanvas<TCanvas>
        {
            if (!TryGetCanvas<TCanvas>(out var canvas))
            {
                reason = $"Canvas of type {typeof(TCanvas).Name} not found";
                return false;
            }

            return canvas.Validate(out reason);
        }

        /// <summary>
        /// Sets cursor visibility and lock mode.
        /// </summary>
        /// <param name="visible">Whether cursor is visible.</param>
        /// <param name="lockMode">Cursor lock mode.</param>
        public void SetCursor(bool visible, CursorLockMode lockMode)
        {
            Cursor.visible = visible;
            Cursor.lockState = lockMode;
        }
    }
}
