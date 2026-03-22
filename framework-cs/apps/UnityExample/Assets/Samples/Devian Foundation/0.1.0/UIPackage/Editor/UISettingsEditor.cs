#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace Devian
{
    [CustomEditor(typeof(UISettings))]
    public sealed class UISettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.UpdateIfRequiredOrScript();

            DrawPropertiesExcluding(serializedObject, "m_Script");

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Popup Frame Mappings", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Auto Fill scans the UI_POPUP_FRAME_ID search directory, reads each popup prefab's actual frame type, and fills missing mappings.",
                EditorStyles.wordWrappedLabel);

            using (new EditorGUI.DisabledScope(target == null))
            {
                if (GUILayout.Button("Auto Fill Missing Popup Mappings"))
                {
                    serializedObject.ApplyModifiedProperties();
                    serializedObject.UpdateIfRequiredOrScript();

                    var mappingsProp = serializedObject.FindProperty("_popupFrameMappings");
                    var changedCount = UIPopupFrameEditorUtility.AutoFillMissingMappings(mappingsProp, out var message);
                    if (changedCount > 0)
                    {
                        EditorUtility.SetDirty(target);
                    }

                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        Debug.Log(message, target);
                        EditorUtility.DisplayDialog("Popup Frame Auto Fill", message, "OK");
                    }
                }
            }

            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }
    }
}

#endif
