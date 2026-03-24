using System.Threading.Tasks;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Central manager for UI Canvas lifecycle.
    /// Provides Canvas lookup, creation, despawn, validation.
    /// AutoSingleton: script-created on first Instance access.
    /// </summary>
    public sealed class UIManager : AutoSingleton<UIManager>
    {
        private UIMessageSystem mMessageSystem = new UIMessageSystem();

        /// <summary>
        /// UI 번들 에셋 로드.
        /// UISettings.UIAddressablesKey를 사용하여 transition preset preload + UI GameObject 로드를 수행한다.
        /// MobileBundleManager.LoadBundlesAsync()에서 호출한다.
        /// </summary>
        public async Task LoadBundlesAsync()
        {
            var uiSettings = Resources.Load<UISettings>(UISettings.ResourcesPath);
            var loadLabel = uiSettings != null && !string.IsNullOrEmpty(uiSettings.UIAddressablesKey)
                ? uiSettings.UIAddressablesKey
                : "ui";

            // 1. transition preset preload
            var presetCacheKey = uiSettings != null
                ? uiSettings.GetSearchDir("UI_TRANSITION_PRESET_ID")
                : string.Empty;
            if (string.IsNullOrWhiteSpace(presetCacheKey))
            {
                presetCacheKey = "UI_TRANSITION_PRESET_ID";
            }

            var presetResult = await AssetManager.LoadBundleAssets<UITransitionPresetAsset>(presetCacheKey, loadLabel);
            if (presetResult.IsFailure)
            {
                Debug.LogWarning($"[UIManager] Failed to preload UI transition presets from '{loadLabel}': {presetResult.Error?.Message}");
            }

            // 2. UI GameObject 로드
            var uiResult = await AssetManager.LoadBundleAssets<GameObject>(loadLabel);
            if (uiResult.IsFailure)
            {
                Debug.LogWarning($"[UIManager] Failed to load UI bundle assets from '{loadLabel}': {uiResult.Error?.Message}");
            }
        }

        /// <summary>
        /// UI message system for UI-level messaging (ReloadText, Resize, etc.).
        /// </summary>
        public static UIMessageSystem messageSystem => Instance?.mMessageSystem;

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
            canvas = FindAnyObjectByType<TCanvas>(FindObjectsInactive.Include);
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
            where TCanvas : MonoBehaviour, IPoolable
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
            where TCanvas : UIBaseCanvas<TCanvas>
        {
            if (!TryGetCanvas<TCanvas>(out var canvas))
            {
                reason = $"Canvas of type {typeof(TCanvas).Name} not found";
                return false;
            }

            return canvas.Validate(out reason);
        }

    }
}
