#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Devian
{
    [CustomEditor(typeof(Devian.CloudSaveManager))]
    public sealed class CloudSaveManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (GUILayout.Button("Generate Key/IV"))
            {
                var mgr = (Devian.CloudSaveManager)target;
                mgr.GenerateKeyIv();
                EditorUtility.SetDirty(mgr);
            }
        }
    }
}
#endif
