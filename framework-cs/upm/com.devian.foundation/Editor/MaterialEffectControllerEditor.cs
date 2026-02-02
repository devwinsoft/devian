using UnityEditor;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// Custom Inspector for MaterialEffectController.
    /// Provides Edit Mode Preview and Snapshot Save functionality.
    /// </summary>
    [CustomEditor(typeof(MaterialEffectController))]
    public sealed class MaterialEffectControllerEditor : Editor
    {
        private MaterialEffectAsset _previewEffect;
        private MaterialSetMaterialEffectAsset _saveTarget;

        public override void OnInspectorGUI()
        {
            // Draw default inspector
            DrawDefaultInspector();

            var controller = (MaterialEffectController)target;
            var renderer = controller.GetComponent<Renderer>();

            EditorGUILayout.Space(10);

            // ===== Preview Section =====
            EditorGUILayout.LabelField("Edit Mode Preview", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledGroupScope(Application.isPlaying))
            {
                _previewEffect = (MaterialEffectAsset)EditorGUILayout.ObjectField(
                    "Preview Effect",
                    _previewEffect,
                    typeof(MaterialEffectAsset),
                    false
                );

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Preview On"))
                {
                    if (_previewEffect != null)
                    {
                        Undo.RecordObject(renderer, "MaterialEffect Preview On");
                        Undo.RecordObject(controller, "MaterialEffect Preview On");
                        controller.EditorPreviewOn(_previewEffect);
                        EditorUtility.SetDirty(renderer);
                    }
                    else
                    {
                        Debug.LogWarning("[MaterialEffectControllerEditor] Please assign a Preview Effect first.");
                    }
                }

                if (GUILayout.Button("Preview Off"))
                {
                    Undo.RecordObject(renderer, "MaterialEffect Preview Off");
                    Undo.RecordObject(controller, "MaterialEffect Preview Off");
                    controller.EditorPreviewOff();
                    EditorUtility.SetDirty(renderer);
                }

                EditorGUILayout.EndHorizontal();

                // Status display
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Status:", GUILayout.Width(50));
                EditorGUILayout.LabelField(
                    controller.EditorPreviewIsActive ? "Preview Active" : "Normal",
                    controller.EditorPreviewIsActive ? EditorStyles.boldLabel : EditorStyles.label
                );
                EditorGUILayout.EndHorizontal();

                if (renderer != null)
                {
                    var mats = renderer.sharedMaterials;
                    EditorGUILayout.LabelField($"Current Materials: {(mats != null ? mats.Length : 0)} slot(s)");
                }
            }

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Preview is disabled during Play Mode.", MessageType.Info);
            }

            EditorGUILayout.Space(10);

            // ===== Snapshot Save Section =====
            EditorGUILayout.LabelField("Snapshot Save", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledGroupScope(Application.isPlaying))
            {
                _saveTarget = (MaterialSetMaterialEffectAsset)EditorGUILayout.ObjectField(
                    "Save Target Asset",
                    _saveTarget,
                    typeof(MaterialSetMaterialEffectAsset),
                    false
                );

                if (GUILayout.Button("Save Snapshot From Renderer"))
                {
                    if (_saveTarget != null)
                    {
                        Undo.RecordObject(_saveTarget, "Save Material Snapshot");
                        controller.EditorSaveSnapshotTo(_saveTarget);
                        AssetDatabase.SaveAssets();
                    }
                    else
                    {
                        Debug.LogWarning("[MaterialEffectControllerEditor] Please assign a Save Target Asset first.");
                    }
                }
            }

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Snapshot Save is disabled during Play Mode.", MessageType.Info);
            }
        }
    }
}
