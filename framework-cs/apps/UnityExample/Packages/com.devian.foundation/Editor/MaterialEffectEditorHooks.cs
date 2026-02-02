using UnityEditor;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Editor hooks for MaterialEffect system.
    /// Automatically turns off all previews before entering Play Mode to prevent baseline contamination.
    /// </summary>
    [InitializeOnLoad]
    public static class MaterialEffectEditorHooks
    {
        static MaterialEffectEditorHooks()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Clean up previews before entering Play Mode
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                CleanupAllPreviews();
            }
        }

        /// <summary>
        /// Find all MaterialEffectControllers in the current scene and turn off their previews.
        /// </summary>
        private static void CleanupAllPreviews()
        {
            var controllers = Object.FindObjectsOfType<MaterialEffectController>(true);
            int cleanedCount = 0;

            foreach (var controller in controllers)
            {
                if (controller.EditorPreviewIsActive)
                {
                    controller.EditorPreviewOff();
                    cleanedCount++;
                }
            }

            if (cleanedCount > 0)
            {
                Debug.Log($"[MaterialEffectEditorHooks] Cleaned up {cleanedCount} active preview(s) before entering Play Mode.");
            }
        }
    }
}
