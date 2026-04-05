#if UNITY_EDITOR

using UnityEditor;

namespace Devian
{
    [CustomEditor(typeof(UISettings))]
    public sealed class UISettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.UpdateIfRequiredOrScript();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();
        }
    }
}

#endif
