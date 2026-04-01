using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace Devian
{
    /// <summary>
    /// BuildAutomationSettings용 Custom Editor.
    /// 섹션별 helpBox 그룹으로 시각적으로 묶고,
    /// 파일/폴더 선택이 필요한 필드에 브라우저 버튼을 제공한다.
    /// </summary>
    [CustomEditor(typeof(BuildAutomationSettings))]
    public class BuildAutomationSettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // ── General ──
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("General", EditorStyles.miniBoldLabel);
                DrawPathFieldInline(
                    serializedObject.FindProperty("buildOutputDir"),
                    "Build Output Dir", isFolder: true);
            }

            EditorGUILayout.Space(4);

            // ── Android ──
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Android", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("includeARMv7"));
                DrawPathFieldInline(
                    serializedObject.FindProperty("keystorePath"),
                    "Keystore Path", isFolder: false, extensions: "keystore,jks");
                DrawPropertyInline(
                    serializedObject.FindProperty("firebaseAndroidAppId"),
                    "Firebase App ID");
            }

            EditorGUILayout.Space(4);

            // ── iOS ──
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("iOS", EditorStyles.miniBoldLabel);
                DrawPropertyInline(
                    serializedObject.FindProperty("firebaseIOSAppId"),
                    "Firebase App ID");
            }

            EditorGUILayout.Space(4);

            // ── Release ──
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Release", EditorStyles.miniBoldLabel);
                DrawPathFieldInline(
                    serializedObject.FindProperty("releaseRepoRoot"),
                    "Release Repo Root", isFolder: true);
                DrawPathFieldInline(
                    serializedObject.FindProperty("versionJsonPathAOS"),
                    "AOS Version JSON", isFolder: false, extensions: "json");
                DrawPathFieldInline(
                    serializedObject.FindProperty("versionJsonPathIOS"),
                    "iOS Version JSON", isFolder: false, extensions: "json");
            }

            EditorGUILayout.Space(4);

            // ── Addressables ──
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Addressables", EditorStyles.miniBoldLabel);
                DrawExcludeGroupsField(serializedObject);
            }

            EditorGUILayout.Space(4);

            // ── CLI Paths ──
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("CLI Paths", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(
                    "비워두면 자동 탐색. 'which firebase'로 경로 확인 가능",
                    EditorStyles.wordWrappedMiniLabel);
                DrawPathFieldInline(
                    serializedObject.FindProperty("firebaseCLIPath"),
                    "Firebase CLI", isFolder: false, extensions: "");
            }

            serializedObject.ApplyModifiedProperties();
        }

        // ─── 공통 헬퍼 ──────────────────────────────────────

        /// <summary>
        /// Label + TextField + [...] 버튼을 한 줄에 배치한다.
        /// </summary>
        private void DrawPathFieldInline(
            SerializedProperty prop, string label,
            bool isFolder, string extensions = "")
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(90));
                prop.stringValue = EditorGUILayout.TextField(prop.stringValue);

                if (GUILayout.Button("...", GUILayout.Width(28)))
                {
                    BrowseAndApply(prop, label, isFolder, extensions);
                }
            }
        }

        /// <summary>
        /// Label + PropertyField를 한 줄에 배치한다.
        /// </summary>
        private void DrawPropertyInline(SerializedProperty prop, string label)
        {
            EditorGUILayout.PropertyField(prop, new GUIContent(label));
        }

        /// <summary>
        /// Addressable Group 제외 목록을 드롭다운 + 리스트로 편집한다.
        /// </summary>
        private void DrawExcludeGroupsField(SerializedObject so)
        {
            var listProp = so.FindProperty("excludedAddressableGroups");
            var settings = (BuildAutomationSettings)target;

            EditorGUILayout.LabelField("Exclude Groups", EditorStyles.miniLabel);

            // 현재 등록된 제외 group 표시
            for (int i = listProp.arraySize - 1; i >= 0; i--)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        $"  ● {listProp.GetArrayElementAtIndex(i).stringValue}");
                    if (GUILayout.Button("✕", GUILayout.Width(22)))
                    {
                        listProp.DeleteArrayElementAtIndex(i);
                    }
                }
            }

            // 드롭다운 — Addressable group 중 아직 제외되지 않은 것만 표시
            var allGroupNames = GetAddressableGroupNames();
            var excluded = new HashSet<string>();
            for (int i = 0; i < listProp.arraySize; i++)
                excluded.Add(listProp.GetArrayElementAtIndex(i).stringValue);

            var available = allGroupNames.Where(n => !excluded.Contains(n)).ToList();
            available.Insert(0, "— Select Group —");

            using (new EditorGUILayout.HorizontalScope())
            {
                var selected = EditorGUILayout.Popup(0, available.ToArray());
                if (selected > 0)
                {
                    listProp.InsertArrayElementAtIndex(listProp.arraySize);
                    listProp.GetArrayElementAtIndex(listProp.arraySize - 1).stringValue =
                        available[selected];
                }

                if (listProp.arraySize > 0 && GUILayout.Button("Clear All", GUILayout.Width(70)))
                {
                    listProp.ClearArray();
                }
            }
        }

        /// <summary>
        /// 현재 Addressables 설정에서 group 이름 목록을 반환한다.
        /// </summary>
        private static List<string> GetAddressableGroupNames()
        {
            var names = new List<string>();
            var aaSettings = AddressableAssetSettingsDefaultObject.Settings;
            if (aaSettings != null)
            {
                foreach (var group in aaSettings.groups)
                {
                    if (group != null)
                        names.Add(group.Name);
                }
            }
            return names;
        }

        /// <summary>
        /// 파일/폴더 브라우저를 열어 선택 결과를 prop에 적용한다.
        /// </summary>
        private void BrowseAndApply(
            SerializedProperty prop, string label,
            bool isFolder, string extensions)
        {
            var currentPath = prop.stringValue;
            var startDir = string.IsNullOrEmpty(currentPath)
                ? Application.dataPath
                : System.IO.Path.GetDirectoryName(currentPath);

            string selected;
            if (isFolder)
            {
                selected = EditorUtility.OpenFolderPanel(label, startDir, "");
            }
            else
            {
                selected = EditorUtility.OpenFilePanel(label, startDir, extensions);
            }

            if (!string.IsNullOrEmpty(selected))
            {
                var projectRoot = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(Application.dataPath, ".."));
                if (selected.StartsWith(projectRoot))
                {
                    selected = selected.Substring(projectRoot.Length + 1);
                }

                prop.stringValue = selected;
            }
        }
    }
}
