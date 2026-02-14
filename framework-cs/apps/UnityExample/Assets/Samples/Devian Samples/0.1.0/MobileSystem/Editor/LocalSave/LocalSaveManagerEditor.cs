#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Devian
{
    [CustomEditor(typeof(Devian.LocalSaveManager))]
    public sealed class LocalSaveManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (GUILayout.Button("Generate Key/IV"))
            {
                var mgr = (Devian.LocalSaveManager)target;
                mgr.GenerateKeyIv();
                EditorUtility.SetDirty(mgr);
            }
        }
    }
}
#endif
