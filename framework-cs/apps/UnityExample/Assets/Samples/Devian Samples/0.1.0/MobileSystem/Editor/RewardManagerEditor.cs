using System;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace Devian
{
    [CustomEditor(typeof(RewardManager))]
    public sealed class RewardManagerEditor : Editor
    {
        const int AesKeySizeBytes = 32;
        const int AesIvSizeBytes = 16;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "Generate key/iv changes encryption config for InitialRewards payloads.",
                MessageType.Info);

            if (GUILayout.Button("Generate key iv"))
            {
                var mgr = (RewardManager)target;
                Undo.RecordObject(mgr, "Generate Rewards Crypto Key/Iv");

                var keyProp = serializedObject.FindProperty("_initialRewardsCryptoKey");
                var ivProp = serializedObject.FindProperty("_initialRewardsCryptoIv");

                setCStringValue(keyProp, generateBase64(AesKeySizeBytes));
                setCStringValue(ivProp, generateBase64(AesIvSizeBytes));

                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(mgr);
                return;
            }

            serializedObject.ApplyModifiedProperties();
        }

        static void setCStringValue(SerializedProperty cstringProp, string plainValue)
        {
            var dataProp = cstringProp?.FindPropertyRelative("data");
            if (dataProp == null) return;

            var cstring = new CString(plainValue);
            dataProp.stringValue = cstring.data;
        }

        static string generateBase64(int sizeBytes)
        {
            var bytes = new byte[sizeBytes];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes);
        }
    }
}
