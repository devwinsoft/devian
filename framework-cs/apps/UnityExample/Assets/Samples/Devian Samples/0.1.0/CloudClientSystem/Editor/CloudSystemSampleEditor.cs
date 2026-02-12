#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Devian
{
    [CustomEditor(typeof(CloudSystemSample))]
    public sealed class CloudSystemSampleEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "CloudSystem is a bundle sample. Importing this installs all sub-codes under Samples~/CloudSystem/.",
                MessageType.Info);

            base.OnInspectorGUI();
        }
    }
}
#endif
