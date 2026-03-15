using System;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace Devian
{
    [CustomEditor(typeof(MobileApplication), true)]
    public sealed class MobileApplicationEditor : Editor
    {
        const int AesKeySizeBytes = 32;
        const int AesIvSizeBytes = 16;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "Generate key/iv changes encryption config for ScriptableObject payloads (FirstRewardSettings, InventorySettings, etc.).",
                MessageType.Info);

            if (GUILayout.Button("Generate key iv"))
            {
                var app = (MobileApplication)target;
                Undo.RecordObject(app, "Generate Crypto Key/Iv");

                var keyProp = serializedObject.FindProperty("_cryptoKey");
                var ivProp = serializedObject.FindProperty("_cryptoIv");

                _setCStringValue(keyProp, _generateBase64(AesKeySizeBytes));
                _setCStringValue(ivProp, _generateBase64(AesIvSizeBytes));

                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(app);
                return;
            }

            serializedObject.ApplyModifiedProperties();
        }

        static void _setCStringValue(SerializedProperty cstringProp, string plainValue)
        {
            var dataProp = cstringProp?.FindPropertyRelative("data");
            if (dataProp == null) return;

            var cstring = new CString(plainValue);
            dataProp.stringValue = cstring.data;
        }

        static string _generateBase64(int sizeBytes)
        {
            var bytes = new byte[sizeBytes];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes);
        }
    }
}
