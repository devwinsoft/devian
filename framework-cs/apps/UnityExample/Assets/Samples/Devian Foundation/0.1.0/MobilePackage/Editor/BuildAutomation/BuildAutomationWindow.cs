using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Devian
{
    /// <summary>
    /// 빌드 자동화 EditorWindow.
    /// 4개 탭(Settings / Pipeline / Release / Log)으로 구성되어
    /// 설정 편집, 빌드 실행, 릴리스(버전 게시 + 심볼 업로드), 실시간 로그를 통합 제공한다.
    /// 메뉴: Devian > Build Automation
    /// </summary>
    public class BuildAutomationWindow : EditorWindow
    {
        private enum Tab { Settings, Pipeline, Release, Log }

        // State
        private Tab _currentTab = Tab.Pipeline;
        private BuildAutomationSettings _settings;
        private Editor _settingsEditor;

        // Pipeline state
        private bool _isRunning;
        private string _currentPhaseLabel = "";
        private float _progress;
        private CancellationTokenSource _pipelineCts;

        // Version / Build Number (편집용 임시 값 — Apply 전까지 실제 값에 반영하지 않음)
        private int _editMajor;
        private int _editMinor;
        private int _editPatch;
        private int _editAndroidBundleCode;
        private string _editIOSBuildNumber = "";
        private bool _versionDirty;

        // 플랫폼 자동감지 (빌드 모듈 설치 여부)
        private bool _androidSupported;
        private bool _iosSupported;

        // 플랫폼 빌드 준비 완료 (모듈 설치 + Settings 구성)
        private bool _androidReady;
        private bool _iosReady;

        // 플랫폼별 빌드 토글 (ready 일 때만 editable, 기본 true)
        private bool _buildAndroid;
        private bool _buildIos;

        // Addressables
        private bool _buildAddressables;
        private int _remoteGroupCount;

        // 플랫폼별 버전 정보 (JSON에서 읽은 값 → 편집용)
        private VersionCheckConfig _lastVersionAOS;
        private VersionCheckConfig _lastVersionIOS;
        private VersionCheckConfig _editVersionAOS;
        private VersionCheckConfig _editVersionIOS;

        // Symbol path (절대 경로 — 빌드 성공 시 자동 입력, 수동 선택 가능)
        private string _androidSymbolPath = "";
        private string _iosSymbolPath = "";

        // Log state
        private Vector2 _logScroll;
        private string _logFilter = "";
        private bool _autoScroll = true;
        private int _lastLogCount;

        // Main scroll (Settings / Pipeline / Release 탭 공용)
        private Vector2 _mainScroll;

        // ═══ IMGUI 스냅샷 ═══
        // IMGUI는 Layout → Repaint 두 패스를 실행한다.
        // 두 패스 사이에 상태가 변하면 컨트롤 수 불일치로
        // EndLayoutGroup 에러가 발생한다.
        // 원인: async continuation, EditorApplication.update, 타이머 등이
        // Layout↔Repaint 사이에 실행될 수 있음.
        //
        // 해결: 컨트롤 수에 영향을 주는 모든 상태를 Layout 패스에서
        // 스냅샷으로 캡처하고, OnGUI 전체에서 스냅샷만 사용한다.

        // 로그 스냅샷
        private BuildLogEntry[] _logSnapshot = System.Array.Empty<BuildLogEntry>();
        private bool _logDirty = true;

        // Pipeline 상태 스냅샷
        private bool _guiIsRunning;
        private string _guiPhaseLabel = "";
        private float _guiProgress;
        private Tab _guiTab;

        // Prerequisites 스냅샷 (firebaseCLI → 조건부 LabelField)
        private PrerequisiteStatus _guiPrereqStatus;

        // Version 스냅샷 (_versionDirty → 조건부 HelpBox)
        private bool _guiVersionDirty;

        // Build 옵션 스냅샷
        private bool _guiDevBuild;
        private bool _guiAppBundle;
        private bool _guiAndroidReady;
        private bool _guiIosReady;
        private bool _guiBuildAndroid;
        private bool _guiBuildIos;
        private bool _guiBuildAddressables;
        private int _guiRemoteGroupCount;
        private VersionCheckConfig _guiLastVersionAOS;
        private VersionCheckConfig _guiLastVersionIOS;
        private VersionCheckConfig _guiEditVersionAOS;
        private VersionCheckConfig _guiEditVersionIOS;

        // Prerequisites cache
        private PrerequisiteStatus _prereqStatus;
        private double _lastPrereqCheck;

        [MenuItem("Devian/Build Automation")]
        public static void Open()
        {
            var window = GetWindow<BuildAutomationWindow>("Build Automation");
            window.minSize = new Vector2(480, 600);
            window.Show();
        }

        private void OnEnable()
        {
            _settings = BuildAutomationUtil.LoadSettings();
            if (_settings != null)
                _settingsEditor = Editor.CreateEditor(_settings);

            // 플랫폼 자동감지
            _androidSupported = BuildPipeline.IsBuildTargetSupported(
                BuildTargetGroup.Android, BuildTarget.Android);
            _iosSupported = BuildPipeline.IsBuildTargetSupported(
                BuildTargetGroup.iOS, BuildTarget.iOS);

            // Prerequisites 초기 검사 → readiness 계산 → 빌드 토글 초기값
            RefreshPrerequisites();
            _buildAndroid = _androidReady;
            _buildIos = _iosReady;

            // Addressables Remote group count
            RefreshRemoteGroupCount();

            // Last Version 로드 (JSON에서 읽기)
            RefreshLastVersions();

            EditorApplication.update += OnEditorUpdate;
            LoadVersion();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            if (_settingsEditor != null)
                DestroyImmediate(_settingsEditor);

            // 창 닫힐 때 실행 중인 프로세스 정리
            _pipelineCts?.Cancel();
            _pipelineCts?.Dispose();
            _pipelineCts = null;
        }

        /// <summary>
        /// 메인 스레드 update에서 백그라운드 로그를 flush하고,
        /// 새 로그가 있으면 dirty 플래그 + Repaint를 한 번만 호출한다.
        /// OnGUI와 완전히 분리된 타이밍에 실행되므로 IMGUI 레이아웃이 안전하다.
        /// </summary>
        private void OnEditorUpdate()
        {
            // 백그라운드 스레드 로그를 메인 스레드로 가져온다
            BuildAutomationLogger.FlushPendingEvents();

            // 로그 수가 변했으면 dirty + Repaint
            var currentCount = BuildAutomationLogger.Count;
            if (currentCount != _lastLogCount)
            {
                _logDirty = true;
                Repaint();
            }

            // Prerequisites 갱신 (OnGUI 내부가 아닌 여기서 수행하여
            // Layout↔Repaint 사이에 _prereqStatus가 바뀌는 것을 방지)
            if (EditorApplication.timeSinceStartup - _lastPrereqCheck > 10)
            {
                RefreshPrerequisites();
                RefreshRemoteGroupCount();
                Repaint();
            }
        }

        private void OnGUI()
        {
            // Layout 패스에서 컨트롤 수에 영향을 주는 모든 상태를 캡처한다.
            if (Event.current.type == EventType.Layout)
            {
                _guiIsRunning = _isRunning;
                _guiPhaseLabel = _currentPhaseLabel;
                _guiProgress = _progress;
                _guiTab = _currentTab;
                _guiPrereqStatus = _prereqStatus;
                _guiVersionDirty = _versionDirty;
                _guiDevBuild = _settings != null && _settings.developmentBuild;
                _guiAppBundle = EditorUserBuildSettings.buildAppBundle;
                _guiAndroidReady = _androidReady;
                _guiIosReady = _iosReady;
                _guiBuildAndroid = _buildAndroid;
                _guiBuildIos = _buildIos;
                _guiBuildAddressables = _buildAddressables;
                _guiRemoteGroupCount = _remoteGroupCount;
                _guiLastVersionAOS = _lastVersionAOS;
                _guiLastVersionIOS = _lastVersionIOS;
                _guiEditVersionAOS = _editVersionAOS;
                _guiEditVersionIOS = _editVersionIOS;
            }

            DrawTabBar();

            if (_settings == null)
            {
                DrawNoSettingsMessage();
                return;
            }

            // 탭 전환도 스냅샷 사용 — async에서 _currentTab 변경 시 안전
            switch (_guiTab)
            {
                case Tab.Settings:
                    _mainScroll = EditorGUILayout.BeginScrollView(_mainScroll);
                    DrawSettingsTab();
                    EditorGUILayout.EndScrollView();
                    break;
                case Tab.Pipeline:
                    _mainScroll = EditorGUILayout.BeginScrollView(_mainScroll);
                    DrawPipelineTab();
                    EditorGUILayout.EndScrollView();
                    break;
                case Tab.Release:
                    _mainScroll = EditorGUILayout.BeginScrollView(_mainScroll);
                    DrawReleaseTab();
                    EditorGUILayout.EndScrollView();
                    break;
                case Tab.Log:
                    // Log 탭은 자체 ScrollView를 사용한다
                    DrawLogTab();
                    break;
            }
        }

        // ─── Tab Bar ────────────────────────────────────────

        private void DrawTabBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Toggle(_currentTab == Tab.Settings, "Settings",
                    EditorStyles.toolbarButton, GUILayout.Width(80)))
                    _currentTab = Tab.Settings;

                if (GUILayout.Toggle(_currentTab == Tab.Pipeline, "Pipeline",
                    EditorStyles.toolbarButton, GUILayout.Width(80)))
                    _currentTab = Tab.Pipeline;

                if (GUILayout.Toggle(_currentTab == Tab.Release, "Release",
                    EditorStyles.toolbarButton, GUILayout.Width(80)))
                    _currentTab = Tab.Release;

                var logLabel = _guiIsRunning
                    ? $"Log ({BuildAutomationLogger.Count})"
                    : "Log";
                if (GUILayout.Toggle(_currentTab == Tab.Log, logLabel,
                    EditorStyles.toolbarButton, GUILayout.Width(80)))
                    _currentTab = Tab.Log;

                GUILayout.FlexibleSpace();

                // 실행 중 표시 — 스냅샷 사용
                if (_guiIsRunning)
                {
                    var style = new GUIStyle(EditorStyles.toolbarButton)
                    {
                        normal = { textColor = new Color(0.2f, 0.8f, 0.2f) }
                    };
                    GUILayout.Label($"● {_guiPhaseLabel}", style);
                }
            }
        }

        // ─── Settings Tab ───────────────────────────────────

        private void DrawSettingsTab()
        {
            EditorGUILayout.Space(4);

            if (_settingsEditor != null)
            {
                _settingsEditor.OnInspectorGUI();
            }

            EditorGUILayout.Space(8);
            DrawPrerequisiteStatus();
        }

        private void DrawPrerequisiteStatus()
        {
            // 타이머 갱신은 OnEditorUpdate에서 수행한다 (OnGUI 내부 금지).

            // 스냅샷 사용 — Layout↔Repaint 간 일관성 보장
            var ps = _guiPrereqStatus;

            EditorGUILayout.LabelField("Prerequisites", EditorStyles.boldLabel);

            DrawStatusRow("Firebase SDK", ps.firebaseSdkFound);

            // Android 섹션 — 모듈 미설치 시 '-' 표시
            DrawStatusRow("Firebase Android App ID",
                ps.androidSupported ? ps.firebaseAndroidAppIdSet : (bool?)null);
            DrawStatusRow("google-services.json",
                ps.androidSupported ? ps.googleServicesJsonFound : (bool?)null);

            // iOS 섹션 — 모듈 미설치 시 '-' 표시
            DrawStatusRow("Firebase iOS App ID",
                ps.iosSupported ? ps.firebaseIOSAppIdSet : (bool?)null);
            DrawStatusRow("GoogleService-Info.plist",
                ps.iosSupported ? ps.googleServiceInfoPlistFound : (bool?)null);

            DrawStatusRow("Firebase CLI", ps.firebaseCLIAvailable);
            EditorGUILayout.LabelField(
                ps.firebaseCLIAvailable
                    ? $"     → {ps.firebaseCLIResolvedPath}"
                    : " ",
                EditorStyles.miniLabel);

            DrawStatusRow("Build Output Dir", ps.buildOutputDirValid);

            DrawStatusRow("Android Module",
                ps.androidSupported ? true : (bool?)null);
            DrawStatusRow("iOS Module",
                ps.iosSupported ? true : (bool?)null);

            // Addressables
            var aaInstalled = AddressableAssetSettingsDefaultObject.Settings != null;
            DrawStatusRow("Addressables", aaInstalled);

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Refresh", GUILayout.Width(80)))
            {
                RefreshPrerequisites();
            }
        }

        private void DrawStatusRow(string label, bool ok)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var icon = ok ? "✅" : "❌";
                EditorGUILayout.LabelField($"  {icon}  {label}");
            }
        }

        /// <summary>
        /// null이면 해당 플랫폼 모듈 미설치 — '—' 표시.
        /// </summary>
        private void DrawStatusRow(string label, bool? ok)
        {
            if (ok.HasValue)
            {
                DrawStatusRow(label, ok.Value);
                return;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"  —  {label} (모듈 미설치)");
            }
        }

        private void RefreshPrerequisites()
        {
            if (_settings != null)
            {
                _prereqStatus = BuildAutomationUtil.CheckPrerequisites(_settings);
                _lastPrereqCheck = EditorApplication.timeSinceStartup;

                // 빌드 준비 = 모듈 설치 + App ID 설정
                _androidReady = _androidSupported &&
                    _prereqStatus.firebaseAndroidAppIdSet;
                _iosReady = _iosSupported &&
                    _prereqStatus.firebaseIOSAppIdSet;

                // not ready → 토글 강제 false
                if (!_androidReady) _buildAndroid = false;
                if (!_iosReady) _buildIos = false;
            }
        }

        // ─── Pipeline Tab ───────────────────────────────────

        private void DrawPipelineTab()
        {
            EditorGUILayout.Space(8);

            // ── Section 1: Version Info ──
            DrawVersionSection();

            DrawSeparator();

            // ── Section 2: Build ──
            DrawBuildSection();

            // ── Progress / Cancel (실행 중일 때만) ──
            if (_guiIsRunning)
            {
                EditorGUILayout.Space(12);
                EditorGUI.ProgressBar(
                    EditorGUILayout.GetControlRect(false, 20),
                    _guiProgress, _guiPhaseLabel);

                EditorGUILayout.Space(4);
                if (GUILayout.Button("■  Cancel", GUILayout.Height(26)))
                {
                    CancelPipeline();
                }
            }
        }

        // ─── Release Tab ──────────────────────────────────────

        private void DrawReleaseTab()
        {
            EditorGUILayout.Space(8);

            // ── Section 1: Version Publish ──
            DrawVersionPublishSection();

            DrawSeparator();

            // ── Section 2: Bundle Upload ──
            DrawBundleUploadSection();

            DrawSeparator();

            // ── Section 3: Symbol Upload ──
            DrawSymbolUploadSection();

            // ── Progress / Cancel (실행 중일 때만) ──
            if (_guiIsRunning)
            {
                EditorGUILayout.Space(12);
                EditorGUI.ProgressBar(
                    EditorGUILayout.GetControlRect(false, 20),
                    _guiProgress, _guiPhaseLabel);

                EditorGUILayout.Space(4);
                if (GUILayout.Button("■  Cancel", GUILayout.Height(26)))
                {
                    CancelPipeline();
                }
            }
        }

        private void DrawVersionPublishSection()
        {
            EditorGUILayout.LabelField("Version Publish", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "버전 JSON을 업데이트하고 git commit합니다. (경로는 Settings에서 설정)",
                EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(8);

            // ── Android ──
            DrawPlatformPublishRow(
                "Android",
                _guiAndroidReady,
                _settings != null ? _settings.versionJsonPathAOS : "",
                _guiLastVersionAOS,
                _editVersionAOS,
                () => RunVersionPublish("AOS"));

            EditorGUILayout.Space(4);

            // ── iOS ──
            DrawPlatformPublishRow(
                "iOS",
                _guiIosReady,
                _settings != null ? _settings.versionJsonPathIOS : "",
                _guiLastVersionIOS,
                _editVersionIOS,
                () => RunVersionPublish("iOS"));

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Refresh Versions", GUILayout.Width(120)))
            {
                RefreshLastVersions();
            }
        }

        /// <summary>
        /// 플랫폼별 버전 편집 + Publish 버튼 행을 그린다.
        /// </summary>
        private void DrawPlatformPublishRow(
            string platform, bool ready, string jsonPath,
            VersionCheckConfig lastVersion,
            VersionCheckConfig editVersion,
            System.Action onPublish)
        {
            EditorGUILayout.LabelField(platform, EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                // JSON 경로 (읽기 전용 — Settings에서 편집)
                EditorGUILayout.LabelField("JSON Path",
                    string.IsNullOrEmpty(jsonPath) ? "(미설정)" : jsonPath);

                // Last Version (JSON에서 읽은 값, 읽기 전용)
                var lastLabel = string.IsNullOrEmpty(lastVersion.currentVersion)
                    ? "(없음)" : lastVersion.currentVersion;
                if (!string.IsNullOrEmpty(lastVersion.minVersion))
                    lastLabel += $"  (min: {lastVersion.minVersion})";
                EditorGUILayout.LabelField("Last Published", lastLabel);

                EditorGUILayout.Space(2);

                // Editable 필드
                using (new EditorGUI.DisabledScope(_guiIsRunning))
                {
                    var newCur = EditorGUILayout.TextField(
                        "Current Version", editVersion.currentVersion ?? "");
                    if (newCur != (editVersion.currentVersion ?? ""))
                        editVersion.currentVersion = newCur;

                    var newMin = EditorGUILayout.TextField(
                        "Min Version", editVersion.minVersion ?? "");
                    if (newMin != (editVersion.minVersion ?? ""))
                        editVersion.minVersion = newMin;
                }
            }

            // Publish 버튼 — 항상 활성 (실행 중이거나 경로 미설정 시만 비활성)
            using (new EditorGUI.DisabledScope(
                _guiIsRunning || string.IsNullOrEmpty(jsonPath)))
            {
                if (GUILayout.Button($"▶  Publish {platform}", GUILayout.Height(26)))
                {
                    onPublish?.Invoke();
                }
            }
        }

        private void DrawSeparator()
        {
            EditorGUILayout.Space(4);
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f, 1f));
            EditorGUILayout.Space(4);
        }

        private void DrawBuildSection()
        {
            EditorGUILayout.LabelField("Build", EditorStyles.boldLabel);

            // 플랫폼 선택 — ready(모듈+설정) 시 editable(기본 true), 아니면 readonly(false)
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!_guiAndroidReady || _guiIsRunning))
                {
                    var android = EditorGUILayout.ToggleLeft(
                        "Android", _guiBuildAndroid, GUILayout.Width(100));
                    if (android != _guiBuildAndroid)
                        _buildAndroid = android;
                }
                using (new EditorGUI.DisabledScope(!_guiIosReady || _guiIsRunning))
                {
                    var ios = EditorGUILayout.ToggleLeft(
                        "iOS", _guiBuildIos, GUILayout.Width(80));
                    if (ios != _guiBuildIos)
                        _buildIos = ios;
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(
                    "Settings 미설정 플랫폼은 비활성",
                    EditorStyles.miniLabel, GUILayout.Width(200));
            }

            EditorGUILayout.Space(4);

            // AAB / APK 선택 — Android 빌드 켜져 있을 때만 편집 가능
            using (new EditorGUI.DisabledScope(!_guiBuildAndroid || _guiIsRunning))
            {
                var appBundle = EditorGUILayout.ToggleLeft(
                    "Build App Bundle (AAB)", _guiAppBundle);
                if (appBundle != _guiAppBundle)
                {
                    EditorUserBuildSettings.buildAppBundle = appBundle;
                }
            }

            // Build Addressable Bundles — 스냅샷 사용
            using (new EditorGUI.DisabledScope(_guiIsRunning))
            {
                var buildAddr = EditorGUILayout.ToggleLeft(
                    "Build Addressable Bundles", _guiBuildAddressables);
                if (buildAddr != _guiBuildAddressables)
                    _buildAddressables = buildAddr;
            }

            // Development Build (Debug Mode) — 스냅샷 사용
            using (new EditorGUI.DisabledScope(_guiIsRunning))
            {
                var devBuild = EditorGUILayout.ToggleLeft(
                    "Development Build (Debug Mode)", _guiDevBuild);
                if (devBuild != _guiDevBuild && _settings != null)
                {
                    _settings.developmentBuild = devBuild;
                    EditorUtility.SetDirty(_settings);
                }
            }

            EditorGUILayout.Space(4);

            using (new EditorGUI.DisabledScope(_guiIsRunning))
            {
                if (GUILayout.Button("▶  Build", GUILayout.Height(30)))
                {
                    // OnGUI 밖에서 실행 — BuildPipeline.BuildPlayer는
                    // OnGUI/player loop 내에서 호출하면 GUI 레이아웃 에러 발생
                    EditorApplication.delayCall += () => RunBuild();
                }
            }
        }

        private void DrawBundleUploadSection()
        {
            EditorGUILayout.LabelField("Bundle Upload", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Remote group의 번들을 release repo에 복사하고 git commit합니다.",
                EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(4);

            // Upload Dir (읽기 전용 — Release Repo Root 표시)
            var releaseRoot = _settings != null ? _settings.releaseRepoRoot : "";
            EditorGUILayout.LabelField("Release Repo Root",
                string.IsNullOrEmpty(releaseRoot) ? "(프로젝트 루트)" : releaseRoot);

            EditorGUILayout.LabelField(
                $"Remote Groups: {_guiRemoteGroupCount}개 감지");

            EditorGUILayout.Space(4);

            using (new EditorGUI.DisabledScope(
                _guiIsRunning || _guiRemoteGroupCount == 0))
            {
                if (GUILayout.Button("⬆  Upload Bundles to Git", GUILayout.Height(30)))
                {
                    EditorApplication.delayCall += () => RunBundleUpload();
                }
            }
        }

        /// <summary>
        /// Remote group 번들을 release repo에 복사하고 git commit한다.
        /// </summary>
        private async void RunBundleUpload()
        {
            _isRunning = true;
            _currentTab = Tab.Log;
            _pipelineCts?.Dispose();
            _pipelineCts = new CancellationTokenSource();
            var ct = _pipelineCts.Token;

            BuildAutomationLogger.Log("=== Bundle Upload started ===");

            try
            {
                _currentPhaseLabel = "Bundle Upload";
                _progress = 0.3f;
                Repaint();

                var ok = await BundleUploadRunner.Run(_settings, ct);
                if (!ok && !ct.IsCancellationRequested)
                {
                    BuildAutomationLogger.LogError("Bundle Upload failed");
                    return;
                }

                if (!ct.IsCancellationRequested)
                    BuildAutomationLogger.Log("=== Bundle Upload completed ===");
            }
            catch (OperationCanceledException)
            {
                BuildAutomationLogger.LogWarning("Bundle Upload cancelled");
            }
            catch (Exception ex)
            {
                BuildAutomationLogger.LogError($"Bundle Upload exception: {ex.Message}");
            }
            finally
            {
                _isRunning = false;
                _currentPhaseLabel = "";
                _progress = 0;
                _pipelineCts?.Dispose();
                _pipelineCts = null;
                Repaint();
            }
        }

        /// <summary>
        /// Addressables Remote group 수를 갱신한다.
        /// </summary>
        private void RefreshRemoteGroupCount()
        {
            _remoteGroupCount = 0;
            var aaSettings = AddressableAssetSettingsDefaultObject.Settings;
            if (aaSettings == null) return;

            foreach (var group in aaSettings.groups)
            {
                if (group == null) continue;
                var schema = group.GetSchema<BundledAssetGroupSchema>();
                if (schema == null) continue;

                var buildPath = schema.BuildPath.GetValue(aaSettings);
                if (buildPath != null && !buildPath.Contains("StreamingAssets"))
                    _remoteGroupCount++;
            }
        }

        private void DrawSymbolUploadSection()
        {
            EditorGUILayout.LabelField("Symbol Upload", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Firebase Crashlytics에 심볼을 업로드합니다. 빌드 성공 시 자동 입력됩니다.",
                EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(4);

            // Android symbol path
            DrawSymbolPathField("Android Symbols", ref _androidSymbolPath,
                isFolder: false, extensions: "zip");

            // iOS dSYM path
            DrawSymbolPathField("iOS dSYMs", ref _iosSymbolPath,
                isFolder: true, extensions: "");

            EditorGUILayout.Space(4);

            using (new EditorGUI.DisabledScope(_guiIsRunning))
            {
                if (GUILayout.Button("⬆  Upload Symbols", GUILayout.Height(30)))
                {
                    RunSymbolUploadOnly();
                }
            }
        }

        private void DrawSymbolPathField(string label, ref string path,
            bool isFolder, string extensions)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(label);
                path = EditorGUILayout.TextField(path);

                if (GUILayout.Button("...", GUILayout.Width(28)))
                {
                    var startDir = string.IsNullOrEmpty(path)
                        ? (_settings != null ? _settings.buildOutputDir : "")
                        : Path.GetDirectoryName(path);

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
                        path = selected;
                }
            }
        }

        // ─── Log Tab ────────────────────────────────────────

        private void DrawLogTab()
        {
            // Layout 패스에서만 스냅샷을 갱신한다.
            // IMGUI는 Layout → Repaint 두 패스를 실행하므로,
            // 두 패스 사이에 엔트리 수가 변하면 레이아웃 에러가 발생한다.
            if (Event.current.type == EventType.Layout && _logDirty)
            {
                var entries = BuildAutomationLogger.Entries;
                _logSnapshot = new BuildLogEntry[entries.Count];
                for (int i = 0; i < entries.Count; i++)
                    _logSnapshot[i] = entries[i];
                _logDirty = false;
            }

            // 상단 도구 바
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Clear",
                    EditorStyles.toolbarButton, GUILayout.Width(50)))
                {
                    BuildAutomationLogger.Clear();
                    _logSnapshot = System.Array.Empty<BuildLogEntry>();
                    _logDirty = false;
                }

                if (GUILayout.Button("Copy All",
                    EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    EditorGUIUtility.systemCopyBuffer =
                        BuildAutomationLogger.GetAllText();
                }

                GUILayout.FlexibleSpace();

                EditorGUILayout.LabelField("Filter:",
                    GUILayout.Width(40));
                _logFilter = EditorGUILayout.TextField(
                    _logFilter, EditorStyles.toolbarSearchField,
                    GUILayout.Width(200));

                _autoScroll = GUILayout.Toggle(
                    _autoScroll, "Auto-scroll",
                    EditorStyles.toolbarButton, GUILayout.Width(80));
            }

            // 로그 본문 — 스냅샷 사용 (Layout/Repaint 간 안정)
            var snapshot = _logSnapshot;

            _logScroll = EditorGUILayout.BeginScrollView(_logScroll);

            for (int i = 0; i < snapshot.Length; i++)
            {
                var entry = snapshot[i];

                // 필터 적용
                if (!string.IsNullOrEmpty(_logFilter) &&
                    entry.Message.IndexOf(_logFilter,
                        StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                // 레벨별 색상
                var style = GetLogStyle(entry.Level);
                EditorGUILayout.LabelField(entry.ToString(), style);
            }

            EditorGUILayout.EndScrollView();

            // Auto-scroll
            if (_autoScroll && snapshot.Length != _lastLogCount)
            {
                _logScroll.y = float.MaxValue;
                _lastLogCount = snapshot.Length;
            }
        }

        private GUIStyle GetLogStyle(BuildLogLevel level)
        {
            var style = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                richText = false,
                fontSize = 11
            };

            switch (level)
            {
                case BuildLogLevel.Warning:
                    style.normal.textColor = new Color(0.9f, 0.8f, 0.2f);
                    break;
                case BuildLogLevel.Error:
                    style.normal.textColor = new Color(1f, 0.3f, 0.3f);
                    break;
                default:
                    style.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
                    break;
            }

            return style;
        }

        // ─── Version Section ─────────────────────────────────

        private void LoadVersion()
        {
            if (VersionNumber.TryParse(PlayerSettings.bundleVersion, out var ver))
            {
                _editMajor = ver.Major;
                _editMinor = ver.Minor;
                _editPatch = ver.Patch;
            }
            else
            {
                _editMajor = 0;
                _editMinor = 0;
                _editPatch = 0;
            }

            _editAndroidBundleCode = PlayerSettings.Android.bundleVersionCode;
            _editIOSBuildNumber = PlayerSettings.iOS.buildNumber ?? "";
            _versionDirty = false;
        }

        private void DrawVersionSection()
        {
            EditorGUILayout.LabelField("Version / Build Number", EditorStyles.boldLabel);

            // AppVersion (Major.Minor.Patch) — SSOT: PlayerSettings.bundleVersion
            EditorGUI.BeginChangeCheck();

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("App Version");
                _editMajor = EditorGUILayout.IntField(_editMajor, GUILayout.Width(50));
                EditorGUILayout.LabelField(".", GUILayout.Width(8));
                _editMinor = EditorGUILayout.IntField(_editMinor, GUILayout.Width(50));
                EditorGUILayout.LabelField(".", GUILayout.Width(8));
                _editPatch = EditorGUILayout.IntField(_editPatch, GUILayout.Width(50));
            }

            // Android / iOS 빌드 번호 — DisabledScope로 감싸서 항상 그린다
            // (조건부로 그리면 androidEnabled/iosEnabled 변경 시 컨트롤 수 불일치)
            using (new EditorGUI.DisabledScope(!_androidSupported))
            {
                if (_androidSupported)
                {
                    _editAndroidBundleCode = EditorGUILayout.IntField(
                        "Android Bundle Version Code", _editAndroidBundleCode);
                }
                else
                {
                    EditorGUILayout.IntField("Android Bundle Version Code", 0);
                }
            }

            using (new EditorGUI.DisabledScope(!_iosSupported))
            {
                if (_iosSupported)
                {
                    _editIOSBuildNumber = EditorGUILayout.TextField(
                        "iOS Build Number", _editIOSBuildNumber);
                }
                else
                {
                    EditorGUILayout.TextField("iOS Build Number", "");
                }
            }

            if (EditorGUI.EndChangeCheck())
                _versionDirty = true;

            // Apply / Revert 버튼
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!_guiVersionDirty))
                {
                    if (GUILayout.Button("Apply", GUILayout.Width(80)))
                    {
                        ApplyVersion();
                    }
                    if (GUILayout.Button("Revert", GUILayout.Width(80)))
                    {
                        LoadVersion();
                    }
                }

                // 현재 적용된 값 표시 — 항상 그린다
                GUILayout.FlexibleSpace();
                var curLabel = $"Current: {PlayerSettings.bundleVersion}";
                EditorGUILayout.LabelField(curLabel,
                    EditorStyles.miniLabel, GUILayout.Width(120));
            }

            // 변경사항 HelpBox — 항상 그린다 (비어있으면 빈 공간)
            if (_guiVersionDirty)
            {
                EditorGUILayout.HelpBox(
                    "변경사항이 있습니다. Apply를 눌러 적용하세요.",
                    MessageType.Warning);
            }
            else
            {
                // 컨트롤 수 유지를 위한 빈 공간
                EditorGUILayout.LabelField(" ", GUILayout.Height(0));
            }
        }

        private void ApplyVersion()
        {
            // PlayerSettings 적용 (bundleVersion + 플랫폼별 빌드 번호) — SSOT
            PlayerSettings.bundleVersion = $"{_editMajor}.{_editMinor}.{_editPatch}";

            if (_androidSupported)
                PlayerSettings.Android.bundleVersionCode = _editAndroidBundleCode;

            if (_iosSupported)
                PlayerSettings.iOS.buildNumber = _editIOSBuildNumber;

            _versionDirty = false;

            BuildAutomationLogger.Log(
                $"[Version] Applied: {_editMajor}.{_editMinor}.{_editPatch}, " +
                $"Android code={_editAndroidBundleCode}, iOS build={_editIOSBuildNumber}");
        }

        // ─── No Settings ───────────────────────────────────

        private void DrawNoSettingsMessage()
        {
            EditorGUILayout.Space(20);
            EditorGUILayout.HelpBox(
                "BuildAutomationSettings.asset not found.\n\n" +
                "Create: Assets > Create > Devian > Build Automation Settings",
                MessageType.Error);

            if (GUILayout.Button("Retry"))
            {
                _settings = BuildAutomationUtil.LoadSettings();
                if (_settings != null)
                    _settingsEditor = Editor.CreateEditor(_settings);
            }
        }

        // ─── Path Resolution ────────────────────

        /// <summary>
        /// Release 데이터 저장소 루트를 반환한다.
        /// releaseRepoRoot가 설정되어 있으면 해당 경로를 사용하고,
        /// 비어있으면 프로젝트 루트를 사용한다.
        /// </summary>
        private string ResolveReleaseRoot()
        {
            if (_settings != null && !string.IsNullOrEmpty(_settings.releaseRepoRoot))
            {
                var root = _settings.releaseRepoRoot;
                if (Path.IsPathRooted(root))
                    return Path.GetFullPath(root);

                // 상대경로면 프로젝트 루트 기준으로 해석
                var projectRoot = Path.GetFullPath(
                    Path.Combine(Application.dataPath, ".."));
                return Path.GetFullPath(Path.Combine(projectRoot, root));
            }

            return Path.GetFullPath(
                Path.Combine(Application.dataPath, ".."));
        }

        // ─── Pipeline / Release Execution ────────────────────

        /// <summary>
        /// 플랫폼별 Version Publish를 실행한다.
        /// Settings에서 설정한 version JSON의 currentVersion을 업데이트하고
        /// git commit 한다. push는 사용자가 직접 수행한다.
        /// </summary>
        /// <param name="platform">"AOS" 또는 "iOS"</param>
        private async void RunVersionPublish(string platform)
        {
            var jsonPath = platform == "AOS"
                ? _settings.versionJsonPathAOS
                : _settings.versionJsonPathIOS;

            if (string.IsNullOrEmpty(jsonPath))
            {
                BuildAutomationLogger.LogError(
                    $"[VersionPublish] {platform} Version JSON 경로가 설정되지 않았습니다. " +
                    "Settings에서 경로를 지정하세요.");
                return;
            }

            _isRunning = true;
            _currentTab = Tab.Log;
            _pipelineCts?.Dispose();
            _pipelineCts = new CancellationTokenSource();
            var ct = _pipelineCts.Token;

            BuildAutomationLogger.Log($"=== Publish Version ({platform}) started ===");

            try
            {
                _currentPhaseLabel = $"Version Publish: {platform}";
                _progress = 0.2f;
                Repaint();

                // 편집 필드에서 값 가져오기
                var editInfo = platform == "AOS" ? _editVersionAOS : _editVersionIOS;

                if (string.IsNullOrEmpty(editInfo.currentVersion))
                {
                    BuildAutomationLogger.LogError(
                        $"[VersionPublish] {platform} Current Version이 비어있습니다.");
                    return;
                }

                // 변경 사항 확인 — last와 동일하면 commit 스킵
                var lastInfo = platform == "AOS" ? _lastVersionAOS : _lastVersionIOS;
                var hasChange =
                    (editInfo.currentVersion ?? "") != (lastInfo.currentVersion ?? "") ||
                    (editInfo.minVersion ?? "") != (lastInfo.minVersion ?? "");

                // version JSON 업데이트
                _currentPhaseLabel = $"Version Publish: {platform} Updating JSON";
                _progress = 0.4f;
                Repaint();

                UpdateVersionJson(jsonPath, editInfo);

                if (ct.IsCancellationRequested) return;

                if (!hasChange)
                {
                    BuildAutomationLogger.Log(
                        $"[VersionPublish] {platform} 변경 사항 없음 — commit 스킵");
                    RefreshLastVersions();
                }
                else
                {
                    // git commit
                    _currentPhaseLabel = $"Version Publish: {platform} Git Commit";
                    _progress = 0.6f;
                    Repaint();

                    var commitMsg =
                        $"chore: bump {platform} version to {editInfo.currentVersion}\n\n" +
                        $"- currentVersion: {lastInfo.currentVersion ?? "(empty)"} → {editInfo.currentVersion}\n" +
                        $"- minVersion: {lastInfo.minVersion ?? "(empty)"} → {editInfo.minVersion}";
                    var files = new[] { jsonPath };

                    var gitOk = await GitRunner.Commit(commitMsg, files, ct, ResolveReleaseRoot());
                    if (!gitOk && !ct.IsCancellationRequested)
                    {
                        BuildAutomationLogger.LogError(
                            $"[VersionPublish] {platform} Git commit failed");
                        return;
                    }

                    if (!ct.IsCancellationRequested)
                    {
                        BuildAutomationLogger.Log(
                            $"=== Publish Version ({platform}) completed: " +
                            $"currentVersion={editInfo.currentVersion}, " +
                            $"minVersion={editInfo.minVersion} ===");
                        RefreshLastVersions();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                BuildAutomationLogger.LogWarning(
                    $"Version Publish ({platform}) cancelled");
            }
            catch (Exception ex)
            {
                BuildAutomationLogger.LogError(
                    $"Version Publish ({platform}) exception: {ex.Message}");
            }
            finally
            {
                _isRunning = false;
                _currentPhaseLabel = "";
                _progress = 0;
                _pipelineCts?.Dispose();
                _pipelineCts = null;
                Repaint();
            }
        }

        /// <summary>
        /// Settings의 JSON 경로에서 Last Version을 읽어 캐시한다.
        /// </summary>
        private void RefreshLastVersions()
        {
            if (_settings == null) return;

            _lastVersionAOS = ReadVersionFromJson(_settings.versionJsonPathAOS);
            _lastVersionIOS = ReadVersionFromJson(_settings.versionJsonPathIOS);

            // JSON에서 읽은 값을 편집 필드 초기값으로 설정 (class이므로 별도 인스턴스 생성)
            _editVersionAOS = new VersionCheckConfig
            {
                currentVersion = _lastVersionAOS.currentVersion,
                minVersion = _lastVersionAOS.minVersion
            };
            _editVersionIOS = new VersionCheckConfig
            {
                currentVersion = _lastVersionIOS.currentVersion,
                minVersion = _lastVersionIOS.minVersion
            };
        }

        /// <summary>
        /// version JSON 파일에서 currentVersion, minVersion을 읽어 반환한다.
        /// 파일이 없거나 파싱 실패 시 빈 값.
        /// </summary>
        private VersionCheckConfig ReadVersionFromJson(string relativePath)
        {
            var info = new VersionCheckConfig();
            if (string.IsNullOrEmpty(relativePath)) return info;

            var releaseRoot = ResolveReleaseRoot();
            var fullPath = Path.Combine(releaseRoot, relativePath);

            if (!File.Exists(fullPath)) return info;

            try
            {
                var json = File.ReadAllText(fullPath);

                var curMatch = System.Text.RegularExpressions.Regex.Match(
                    json, "\"currentVersion\"\\s*:\\s*\"([^\"]*)\"");
                if (curMatch.Success)
                    info.currentVersion = curMatch.Groups[1].Value;

                var minMatch = System.Text.RegularExpressions.Regex.Match(
                    json, "\"minVersion\"\\s*:\\s*\"([^\"]*)\"");
                if (minMatch.Success)
                    info.minVersion = minMatch.Groups[1].Value;

                return info;
            }
            catch
            {
                return info;
            }
        }

        /// <summary>
        /// version JSON 파일의 currentVersion, minVersion을 업데이트한다.
        /// </summary>
        private void UpdateVersionJson(string relativePath, VersionCheckConfig info)
        {
            var releaseRoot = ResolveReleaseRoot();
            var fullPath = Path.Combine(releaseRoot, relativePath);

            if (!File.Exists(fullPath))
            {
                BuildAutomationLogger.LogWarning(
                    $"[VersionPublish] File not found, creating: {relativePath}");
                var dir = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
            }

            try
            {
                string json;
                if (File.Exists(fullPath))
                {
                    json = File.ReadAllText(fullPath);
                }
                else
                {
                    json = "{}";
                }

                // 간단한 JSON 문자열 치환 — Newtonsoft 의존 없이 처리
                json = ReplaceOrInsertJsonField(
                    json, "currentVersion", info.currentVersion ?? "");
                json = ReplaceOrInsertJsonField(
                    json, "minVersion", info.minVersion ?? "");

                File.WriteAllText(fullPath, json);
                BuildAutomationLogger.Log(
                    $"[VersionPublish] Updated {relativePath}: " +
                    $"currentVersion={info.currentVersion}, minVersion={info.minVersion}");
            }
            catch (Exception ex)
            {
                BuildAutomationLogger.LogError(
                    $"[VersionPublish] Failed to update {relativePath}: {ex.Message}");
            }
        }

        /// <summary>
        /// JSON 문자열에서 필드를 치환하거나, 없으면 삽입한다.
        /// </summary>
        private static string ReplaceOrInsertJsonField(
            string json, string fieldName, string value)
        {
            var pattern = $"\"{fieldName}\"\\s*:\\s*\"[^\"]*\"";
            if (System.Text.RegularExpressions.Regex.IsMatch(json, pattern))
            {
                return System.Text.RegularExpressions.Regex.Replace(
                    json, pattern, $"\"{fieldName}\": \"{value}\"");
            }

            // 필드가 없으면 첫 번째 { 뒤에 삽입
            var idx = json.IndexOf('{');
            if (idx >= 0)
            {
                json = json.Insert(idx + 1,
                    $"\n  \"{fieldName}\": \"{value}\",");
            }
            return json;
        }

        /// <summary>
        /// 빌드를 실행한다.
        /// </summary>
        private async void RunBuild()
        {
            _isRunning = true;
            _currentTab = Tab.Log;
            _pipelineCts?.Dispose();
            _pipelineCts = new CancellationTokenSource();
            var ct = _pipelineCts.Token;

            BuildAutomationLogger.Log("=== Build started ===");

            try
            {
                // Addressable Bundle Build (체크 시)
                if (_buildAddressables)
                {
                    _currentPhaseLabel = "Addressable Bundle Build";
                    _progress = 0.05f;
                    Repaint();

                    var excludeList = _settings != null
                        ? _settings.excludedAddressableGroups
                        : new List<string>();

                    var bundlePath = BundleBuildRunner.Run(excludeList, _settings);
                    if (bundlePath == null)
                    {
                        BuildAutomationLogger.LogError(
                            "Addressable Bundle Build failed — 앱 빌드를 중단합니다.");
                        return;
                    }
                    BuildAutomationLogger.Log(
                        $"Addressable Bundle Build completed: {bundlePath}");

                    if (ct.IsCancellationRequested) return;

                    // Bundle Copy — 빌드 산출물을 release repo에 복사 + git commit
                    _currentPhaseLabel = "Bundle Copy";
                    _progress = 0.1f;
                    Repaint();

                    var copyOk = await BundleUploadRunner.Run(_settings, ct);
                    if (!copyOk && !ct.IsCancellationRequested)
                    {
                        BuildAutomationLogger.LogWarning(
                            "Bundle Copy failed — 앱 빌드는 계속 진행합니다.");
                    }
                }

                if (ct.IsCancellationRequested) return;

                var buildOk = await RunPhase1();
                if (!buildOk || ct.IsCancellationRequested)
                {
                    if (!ct.IsCancellationRequested)
                        BuildAutomationLogger.LogError("Build failed");
                    return;
                }

                BuildAutomationLogger.Log("=== Build completed ===");

                // 빌드 성공 후 심볼 경로 자동 입력
                AutoFillSymbolPaths();
            }
            catch (OperationCanceledException)
            {
                BuildAutomationLogger.LogWarning("Build cancelled");
            }
            catch (Exception ex)
            {
                BuildAutomationLogger.LogError($"Build exception: {ex.Message}");
            }
            finally
            {
                _isRunning = false;
                _currentPhaseLabel = "";
                _progress = 0;
                _pipelineCts?.Dispose();
                _pipelineCts = null;
                Repaint();
            }
        }

        /// <summary>
        /// Phase 2 (Symbol Upload)를 빌드 없이 독립 실행한다.
        /// 기존 빌드 산출물의 심볼을 Firebase Crashlytics에 업로드한다.
        /// </summary>
        private async void RunSymbolUploadOnly()
        {
            _isRunning = true;
            _currentTab = Tab.Log;
            _pipelineCts?.Dispose();
            _pipelineCts = new CancellationTokenSource();
            var ct = _pipelineCts.Token;

            BuildAutomationLogger.Log("=== Symbol Upload (standalone) started ===");

            try
            {
                var symbolOk = await RunPhase2(ct);
                if (!symbolOk && !ct.IsCancellationRequested)
                    BuildAutomationLogger.LogError("Symbol Upload failed");

                if (!ct.IsCancellationRequested && symbolOk)
                    BuildAutomationLogger.Log("=== Symbol Upload (standalone) completed ===");
            }
            catch (OperationCanceledException)
            {
                BuildAutomationLogger.LogWarning("Symbol Upload cancelled");
            }
            catch (Exception ex)
            {
                BuildAutomationLogger.LogError($"Symbol Upload exception: {ex.Message}");
            }
            finally
            {
                _isRunning = false;
                _currentPhaseLabel = "";
                _progress = 0;
                _pipelineCts?.Dispose();
                _pipelineCts = null;
                Repaint();
            }
        }

        private async Task<bool> RunPhase1()
        {
            _currentPhaseLabel = "Phase 1: Build";
            _progress = 0.1f;
            Repaint();

            var allOk = true;

            if (_buildAndroid)
            {
                _currentPhaseLabel = "Phase 1: Android Build";
                _progress = 0.2f;
                Repaint();

                var report = AndroidBuildRunner.Run(_settings);
                if (report == null || report.summary.result != BuildResult.Succeeded)
                    allOk = false;
            }

            if (_buildIos && allOk)
            {
                _currentPhaseLabel = "Phase 1: iOS Build";
                _progress = 0.5f;
                Repaint();

                var report = IOSBuildRunner.Run(_settings);
                if (report == null || report.summary.result != BuildResult.Succeeded)
                    allOk = false;
            }

            _progress = 0.7f;
            Repaint();

            return allOk;
        }

        private async Task<bool> RunPhase2(CancellationToken ct)
        {
            _currentPhaseLabel = "Symbol Upload";
            _progress = 0.75f;
            Repaint();

            var allOk = true;

            if (_buildAndroid)
            {
                if (string.IsNullOrEmpty(_androidSymbolPath))
                {
                    BuildAutomationLogger.LogError(
                        "[SymbolUpload] Android symbol path is empty. " +
                        "빌드를 먼저 실행하거나, Release 탭에서 경로를 직접 지정하세요.");
                    allOk = false;
                }
                else if (!await SymbolUploader.UploadAndroid(
                    _settings, _androidSymbolPath, ct))
                {
                    allOk = false;
                }
            }

            if (ct.IsCancellationRequested) return false;

            if (_buildIos)
            {
                if (string.IsNullOrEmpty(_iosSymbolPath))
                {
                    BuildAutomationLogger.LogError(
                        "[SymbolUpload] iOS dSYM path is empty. " +
                        "빌드를 먼저 실행하거나, Release 탭에서 경로를 직접 지정하세요.");
                    allOk = false;
                }
                else if (!await SymbolUploader.UploadIOS(
                    _settings, _iosSymbolPath, ct))
                {
                    allOk = false;
                }
            }

            _progress = 0.9f;
            Repaint();

            return allOk;
        }

        /// <summary>
        /// 빌드 산출물 디렉토리에서 심볼 파일 경로를 자동 탐색하여 필드에 채운다.
        /// </summary>
        private void AutoFillSymbolPaths()
        {
            var projectRoot = Path.GetFullPath(
                Path.Combine(Application.dataPath, ".."));
            var outputDir = Path.IsPathRooted(_settings.buildOutputDir)
                ? _settings.buildOutputDir
                : Path.Combine(projectRoot, _settings.buildOutputDir);

            // Android: symbols.zip 탐색
            if (_buildAndroid)
            {
                var androidDir = Path.Combine(outputDir, "Android");
                var found = FindSymbolsZip(androidDir);
                if (found != null)
                {
                    _androidSymbolPath = found;
                    BuildAutomationLogger.Log(
                        $"[SymbolUpload] Android symbol path auto-filled: {found}");
                }
                else
                {
                    BuildAutomationLogger.LogWarning(
                        $"[SymbolUpload] symbols.zip not found in {androidDir}");
                }
            }

            // iOS: dSYMs 디렉토리 탐색
            if (_buildIos)
            {
                var dsymDir = Path.Combine(outputDir, "iOS", "app.xcarchive", "dSYMs");
                if (Directory.Exists(dsymDir))
                {
                    _iosSymbolPath = dsymDir;
                    BuildAutomationLogger.Log(
                        $"[SymbolUpload] iOS dSYM path auto-filled: {dsymDir}");
                }
                else
                {
                    BuildAutomationLogger.LogWarning(
                        $"[SymbolUpload] dSYMs directory not found: {dsymDir}");
                }
            }

            Repaint();
        }

        private static string FindSymbolsZip(string dir)
        {
            if (!Directory.Exists(dir)) return null;

            var files = Directory.GetFiles(dir, "*.symbols.zip");
            if (files.Length > 0) return files[0];

            files = Directory.GetFiles(dir, "*symbols*.zip");
            return files.Length > 0 ? files[0] : null;
        }

        private void CancelPipeline()
        {
            BuildAutomationLogger.LogWarning("Pipeline cancelling...");

            // 1. CancellationToken을 통해 async 체인에 취소 신호 전달
            _pipelineCts?.Cancel();

            // 2. 현재 실행 중인 외부 프로세스를 즉시 강제 종료
            SymbolUploader.Kill();
            GitRunner.Kill();

            _isRunning = false;
            _currentPhaseLabel = "";
            _progress = 0;
            BuildAutomationLogger.LogWarning("Pipeline cancelled by user");
            Repaint();
        }
    }

}
