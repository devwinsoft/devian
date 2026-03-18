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

            // ── Fix URL ──
            DrawFixUrlSection();

            EditorGUILayout.Space(8f);

            // ── Generate Key/IV ──
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

        void DrawFixUrlSection()
        {
            var aosProp = serializedObject.FindProperty("VersionCheckAOS");
            var iosProp = serializedObject.FindProperty("VersionCheckIOS");
            if (aosProp == null || iosProp == null) return;

            var aosUrl = aosProp.stringValue ?? "";
            var iosUrl = iosProp.stringValue ?? "";

            var aosNeedsFix = NeedsUrlFix(aosUrl);
            var iosNeedsFix = NeedsUrlFix(iosUrl);

            EditorGUILayout.Space(4f);

            if (aosNeedsFix || iosNeedsFix)
            {
                EditorGUILayout.HelpBox(
                    "VersionCheck URL이 GitHub blob URL입니다.\n" +
                    "raw.githubusercontent.com URL로 변환해야 앱에서 JSON을 받을 수 있습니다.",
                    MessageType.Warning);

                if (GUILayout.Button("Fix URL"))
                {
                    Undo.RecordObject(target, "Fix VersionCheck URLs");

                    if (aosNeedsFix)
                        aosProp.stringValue = FixGitHubUrl(aosUrl);
                    if (iosNeedsFix)
                        iosProp.stringValue = FixGitHubUrl(iosUrl);

                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(target);
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "VersionCheck URL 상태: 정상",
                    MessageType.Info);
            }
        }

        /// <summary>
        /// github.com/.../blob/... URL인지 검사한다.
        /// </summary>
        static bool NeedsUrlFix(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            return url.Contains("github.com/") && url.Contains("/blob/");
        }

        /// <summary>
        /// GitHub blob URL → raw.githubusercontent.com URL로 변환한다.
        /// 예: https://github.com/user/repo/blob/main/path/file.json
        ///   → https://raw.githubusercontent.com/user/repo/main/path/file.json
        /// </summary>
        static string FixGitHubUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            return url
                .Replace("github.com/", "raw.githubusercontent.com/")
                .Replace("/blob/", "/");
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
