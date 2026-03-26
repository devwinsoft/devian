using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using Debug = UnityEngine.Debug;

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

                // Keystore Credentials (EditorPrefs — git에 포함되지 않음)
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField(
                    "Keystore Credentials (EditorPrefs — 로컬 전용, git 미포함)",
                    EditorStyles.wordWrappedMiniLabel);
                DrawEditorPrefsPassword("Keystore Pass",
                    () => BuildAutomationSettings.KeystorePass,
                    v => BuildAutomationSettings.KeystorePass = v);

                // Key Alias Name — 자동 조회 버튼 포함
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Key Alias Name", GUILayout.Width(100));
                    var kaName = BuildAutomationSettings.KeyaliasName;
                    var newKaName = EditorGUILayout.TextField(kaName);
                    if (newKaName != kaName) BuildAutomationSettings.KeyaliasName = newKaName;

                    var keystorePath = serializedObject.FindProperty("keystorePath").stringValue;
                    var canDetect = !string.IsNullOrEmpty(keystorePath)
                        && !string.IsNullOrEmpty(BuildAutomationSettings.KeystorePass);
                    using (new EditorGUI.DisabledScope(!canDetect))
                    {
                        if (GUILayout.Button("Detect", GUILayout.Width(52)))
                        {
                            var alias = DetectKeyAlias(keystorePath,
                                BuildAutomationSettings.KeystorePass);
                            if (alias != null)
                            {
                                BuildAutomationSettings.KeyaliasName = alias;
                                Debug.Log($"[BuildAutomation] Detected key alias: {alias}");
                            }
                        }
                    }
                }

                DrawEditorPrefsPassword("Key Alias Pass",
                    () => BuildAutomationSettings.KeyaliasPass,
                    v => BuildAutomationSettings.KeyaliasPass = v);

                EditorGUILayout.Space(2);
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

                var cdnProp = serializedObject.FindProperty("remoteCdnUrl");

                // remoteCdnUrl이 비어있으면 Addressables Profile에서 자동 추출
                if (string.IsNullOrEmpty(cdnProp.stringValue))
                {
                    var extracted = ExtractCdnUrlFromProfile();
                    if (!string.IsNullOrEmpty(extracted))
                    {
                        cdnProp.stringValue = extracted;
                        Debug.Log($"[BuildAutomation] Remote CDN URL auto-filled from Addressables Profile: {extracted}");
                    }
                }

                var prevCdn = cdnProp.stringValue;
                DrawPropertyInline(cdnProp, "Remote CDN URL");

                // 값 변경 시 Addressables Profile 즉시 동기화
                if (cdnProp.stringValue != prevCdn)
                {
                    // ApplyModifiedProperties 전에 동기화 예약
                    EditorApplication.delayCall += () => SyncRemoteLoadPathToProfile(cdnProp.stringValue);
                }

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
        /// EditorPrefs 기반 텍스트 필드 (평문).
        /// </summary>
        private static void DrawEditorPrefsField(
            string label, System.Func<string> getter, System.Action<string> setter)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(100));
                var current = getter();
                var next = EditorGUILayout.TextField(current);
                if (next != current) setter(next);
            }
        }

        /// <summary>
        /// EditorPrefs 기반 패스워드 필드 (마스킹).
        /// </summary>
        private static void DrawEditorPrefsPassword(
            string label, System.Func<string> getter, System.Action<string> setter)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(100));
                var current = getter();
                var next = EditorGUILayout.PasswordField(current);
                if (next != current) setter(next);
            }
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

        /// <summary>
        /// Addressables Profile의 Remote.LoadPath에서 CDN base URL을 추출한다.
        /// raw 형식: "https://xxx.cloudfront.net/v[...]/[BuildTarget]"
        /// → "https://xxx.cloudfront.net" 부분만 반환.
        /// </summary>
        private static string ExtractCdnUrlFromProfile()
        {
            var aaSettings = AddressableAssetSettingsDefaultObject.Settings;
            if (aaSettings == null) return null;

            var profileId = aaSettings.activeProfileId;
            var raw = aaSettings.profileSettings.GetValueByName(profileId, "Remote.LoadPath");
            if (string.IsNullOrEmpty(raw) || !raw.StartsWith("http")) return null;

            // "/v[" 또는 "/v0" 등 버전 prefix 시작점을 찾아 그 앞까지가 CDN URL
            var vIdx = raw.IndexOf("/v[");
            if (vIdx < 0) vIdx = raw.IndexOf("/v0");
            if (vIdx < 0) vIdx = raw.IndexOf("/v1");
            if (vIdx < 0) vIdx = raw.IndexOf("/v2");
            if (vIdx < 0)
            {
                // /v prefix 없으면 마지막 /[BuildTarget] 앞까지
                var lastSlash = raw.LastIndexOf('/');
                if (lastSlash > 8) // "https://" 이후
                    return raw.Substring(0, lastSlash);
                return null;
            }

            return raw.Substring(0, vIdx);
        }

        /// <summary>
        /// Addressables Profile의 Remote.LoadPath를
        /// {remoteCdnUrl}/v[bundleVersion]/[BuildTarget] 형태로 동기화한다.
        /// </summary>
        private static void SyncRemoteLoadPathToProfile(string remoteCdnUrl)
        {
            if (string.IsNullOrEmpty(remoteCdnUrl)) return;

            var aaSettings = AddressableAssetSettingsDefaultObject.Settings;
            if (aaSettings == null) return;

            var cdnBase = remoteCdnUrl.TrimEnd('/');
            var expected = $"{cdnBase}/v[UnityEditor.PlayerSettings.bundleVersion]/[BuildTarget]";

            var profileId = aaSettings.activeProfileId;
            var current = aaSettings.profileSettings.GetValueByName(profileId, "Remote.LoadPath");

            if (current == expected) return;

            aaSettings.profileSettings.SetValue(profileId, "Remote.LoadPath", expected);
            EditorUtility.SetDirty(aaSettings);
            Debug.Log($"[BuildAutomation] Remote.LoadPath synced → {expected}");
        }

        /// <summary>
        /// keytool -list로 keystore의 첫 번째 PrivateKeyEntry alias를 반환한다.
        /// </summary>
        private static string DetectKeyAlias(string keystorePath, string keystorePass)
        {
            // keystorePath가 상대경로면 프로젝트 루트 기준으로 절대경로 변환
            if (!System.IO.Path.IsPathRooted(keystorePath))
            {
                var projectRoot = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(Application.dataPath, ".."));
                keystorePath = System.IO.Path.Combine(projectRoot, keystorePath);
            }

            if (!System.IO.File.Exists(keystorePath))
            {
                Debug.LogError($"[BuildAutomation] Keystore not found: {keystorePath}");
                return null;
            }

            // keytool 경로 탐색
            var keytoolPath = ResolveKeytool();
            if (keytoolPath == null)
            {
                Debug.LogError("[BuildAutomation] keytool not found. JDK가 설치되어 있는지 확인하세요.");
                return null;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = keytoolPath,
                    Arguments = $"-list -keystore \"{keystorePath}\" -storepass \"{keystorePass}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var process = new Process { StartInfo = startInfo };
                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit(10000);

                if (process.ExitCode != 0)
                {
                    Debug.LogError($"[BuildAutomation] keytool failed: {error.Trim()}");
                    return null;
                }

                // "aliasName, date, PrivateKeyEntry," 형태에서 alias 추출
                foreach (var line in output.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.Contains("PrivateKeyEntry"))
                    {
                        var commaIdx = trimmed.IndexOf(',');
                        if (commaIdx > 0)
                        {
                            return trimmed.Substring(0, commaIdx).Trim();
                        }
                    }
                }

                Debug.LogWarning("[BuildAutomation] No PrivateKeyEntry found in keystore.");
                return null;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BuildAutomation] keytool error: {ex.Message}");
                return null;
            }
        }

        /// <summary>keytool 실행 파일 경로를 찾는다.</summary>
        private static string ResolveKeytool()
        {
            // Unity가 사용하는 JDK 경로에서 먼저 탐색
            var jdkPath = EditorPrefs.GetString("JdkPath", "");
            if (!string.IsNullOrEmpty(jdkPath))
            {
                var candidate = System.IO.Path.Combine(jdkPath, "bin", "keytool");
                if (System.IO.File.Exists(candidate)) return candidate;
            }

            // 시스템 경로 탐색
            var searchPaths = new[]
            {
                "/usr/bin/keytool",
                "/usr/local/bin/keytool",
                "/opt/homebrew/bin/keytool",
            };
            foreach (var p in searchPaths)
            {
                if (System.IO.File.Exists(p)) return p;
            }

            // which로 탐색
            try
            {
                var si = new ProcessStartInfo
                {
                    FileName = "/usr/bin/which",
                    Arguments = "keytool",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var proc = new Process { StartInfo = si };
                proc.Start();
                var result = proc.StandardOutput.ReadToEnd().Trim();
                proc.WaitForExit(5000);
                if (proc.ExitCode == 0 && System.IO.File.Exists(result))
                    return result;
            }
            catch { /* ignore */ }

            return null;
        }
    }
}
