using System;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace Devian
{
    [CustomEditor(typeof(InventoryManager))]
    public sealed class InventoryManagerEditor : Editor
    {
        const int AesKeySizeBytes = 32;
        const int AesIvSizeBytes = 16;

        SerializedProperty _keyProp;
        SerializedProperty _ivProp;

        void OnEnable()
        {
            _keyProp = serializedObject.FindProperty("_initialInventoryCryptoKey");
            _ivProp = serializedObject.FindProperty("_initialInventoryCryptoIv");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "Generate key/iv changes encryption config for InitialInventory payloads.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(_keyProp == null || _ivProp == null))
            {
                if (GUILayout.Button("Generate key iv"))
                {
                    Undo.RecordObject(target, "Generate Inventory Crypto Key/Iv");
                    _keyProp.stringValue = generateBase64(AesKeySizeBytes);
                    _ivProp.stringValue = generateBase64(AesIvSizeBytes);
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(target);
                    return;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        static string generateBase64(int sizeBytes)
        {
            var bytes = new byte[sizeBytes];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes);
        }
    }
}
