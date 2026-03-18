# 50-editor-window — Build Operation GUI

Status: ACTIVE
AppliesTo: v11

Unity EditorWindow로 구현하는 빌드 및 릴리스 운영 GUI.
설정 편집, 빌드 실행, 릴리스(버전 게시 + 심볼 업로드), 실시간 로그를 하나의 창에서 제공한다.

---

## Window 구조

EditorWindow는 4개 탭으로 구성한다:

```
┌──────────────────────────────────────────────────┐
│ [Settings] [Pipeline] [Release] [Log] ● Running  │
├──────────────────────────────────────────────────┤
│                                                  │
│              (탭별 내용, ScrollView)               │
│                                                  │
└──────────────────────────────────────────────────┘
```

메뉴 접근: `Devian > Build Automation`

---

## Tab 1: Settings

`BuildAutomationSettings` ScriptableObject를 인라인 편집한다.
각 섹션은 `EditorStyles.helpBox` VerticalScope GroupBox로 시각적으로 묶는다.

```
┌─ Settings ───────────────────────────────────────┐
│                                                  │
│  ┌─ General ─────────────────────────────────┐   │
│  │ Build Output Dir  [Builds          ] [...] │   │
│  └───────────────────────────────────────────┘   │
│                                                  │
│  ┌─ Android ─────────────────────────────────┐   │
│  │ ☐ Include ARMv7                            │   │
│  │ Keystore Path     [                ] [...] │   │
│  │ Firebase App ID   [1:xxx:android:xx]       │   │
│  └───────────────────────────────────────────┘   │
│                                                  │
│  ┌─ iOS ─────────────────────────────────────┐   │
│  │ Firebase App ID   [1:xxx:ios:xxxxx]        │   │
│  └───────────────────────────────────────────┘   │
│                                                  │
│  ┌─ Version JSON ────────────────────────────┐   │
│  │ AOS  [release/version_aos.json]     [...] │   │
│  │ iOS  [release/version_ios.json]     [...] │   │
│  └───────────────────────────────────────────┘   │
│                                                  │
│  ┌─ CLI Paths ───────────────────────────────┐   │
│  │ 비워두면 자동 탐색. 'which firebase'로 확인  │   │
│  │ Firebase CLI  [                ]    [...]  │   │
│  └───────────────────────────────────────────┘   │
│                                                  │
│  ┌─ Pipeline Options ────────────────────────┐   │
│  │ ☐ Development Build (Debug Mode)           │   │
│  └───────────────────────────────────────────┘   │
│                                                  │
│  ── Prerequisites ──                             │
│  ✅ Firebase SDK                                 │
│  ✅ Firebase Android App ID                      │
│  ✅ Firebase iOS App ID                          │
│  ✅ google-services.json                         │
│  ✅ GoogleService-Info.plist                     │
│  ✅ Firebase CLI  → /usr/local/bin/firebase      │
│  ✅ Build Output Dir                             │
│  ✅ Android Module                               │
│  ✅ iOS Module                                   │
│  [Refresh]                                       │
│                                                  │
└──────────────────────────────────────────────────┘
```

Prerequisites 상태는 `OnEditorUpdate`에서 10초 간격으로 갱신한다 (OnGUI 내부 금지).
플랫폼 모듈 미설치 시 해당 항목은 `— (모듈 미설치)`로 표시 (`DrawStatusRow(string, bool?)` null 오버로드).

---

## Tab 2: Pipeline

2개 섹션으로 구성: Version Info / Build.
Settings/Pipeline/Release 탭은 공용 `_mainScroll` ScrollView로 감싼다.

```
┌─ Pipeline ───────────────────────────────────────┐
│                                                  │
│  ── Version Info ──                              │
│  App Version    [1].[0].[0]                      │
│  Android Code   [1        ]                      │
│  iOS Build      [1.0.0    ]                      │
│  [Apply] [Revert]          Current: 1.0.0        │
│  ─────────────────────────────────────────────   │
│                                                  │
│  ── Build ──                                     │
│  ☑ Android  ☑ iOS  (readiness 기반 editable)      │
│  ☑ Build App Bundle (AAB)                        │
│  ☐ Development Build (Debug Mode)                │
│  [▶  Build]                                      │
│                                                  │
│  ── (실행 중일 때만) ──                            │
│  ████████████░░░░░░  75%  Building...            │
│  [■  Cancel]                                     │
│                                                  │
└──────────────────────────────────────────────────┘
```

### 플랫폼 Readiness

플랫폼 토글은 3단계 개념으로 분리한다:

| 개념 | 변수 | 판정 기준 | 용도 |
|------|------|-----------|------|
| Module Supported | `_androidSupported` / `_iosSupported` | `BuildPipeline.IsBuildTargetSupported()` | Version 섹션 DisabledScope, Prerequisites 표시 |
| Build Ready | `_androidReady` / `_iosReady` | supported AND `firebaseAppIdSet` | 토글 editable 여부 결정 |
| Build Selected | `_buildAndroid` / `_buildIos` | 사용자가 체크 (ready일 때만 editable) | 실제 빌드 실행 대상 |

- Ready가 아닌 플랫폼: 토글 `false` + `DisabledScope` (읽기 전용)
- Ready인 플랫폼: 토글 editable, 초기값 `true`
- `RefreshPrerequisites()`에서 readiness 재계산. not ready로 바뀌면 `_buildAndroid = false` 강제.

### 실행 로직

**Build App Bundle (AAB) 체크박스**: `EditorUserBuildSettings.buildAppBundle`을 토글한다. 스냅샷 `_guiAppBundle` 사용. `_guiBuildAndroid`가 false이면 비활성화. 실행 중에는 비활성화.

**Development Build 체크박스**: `settings.developmentBuild`를 토글한다. 스냅샷 `_guiDevBuild` 사용. 실행 중에는 비활성화.

**Build 버튼** (`RunBuild()`):
1. `_buildAndroid`/`_buildIos`가 true인 플랫폼만 빌드
2. 빌드 성공 시 `AutoFillSymbolPaths()`로 심볼 경로 자동 입력

---

## Tab 3: Release

2개 섹션으로 구성: Version Publish / Symbol Upload.

```
┌─ Release ────────────────────────────────────────┐
│                                                  │
│  ── Version Publish ──                           │
│                                                  │
│   ── Android ──                                  │
│   Last Published  current: 1.2.3  min: 1.0.0     │
│   Current Version [1.2.3      ]                  │
│   Min Version     [1.0.0      ]                  │
│   [▶  Publish]                                   │
│                                                  │
│   ── iOS ──                                      │
│   Last Published  current: 1.2.3  min: 1.0.0     │
│   Current Version [1.2.3      ]                  │
│   Min Version     [1.0.0      ]                  │
│   [▶  Publish]                                   │
│  ─────────────────────────────────────────────   │
│                                                  │
│  ── Symbol Upload ──                             │
│  Android Symbols  [/abs/path/symbols.zip] [...]  │
│  iOS dSYMs        [/abs/path/dSYMs      ] [...]  │
│  [⬆  Upload Symbols]                            │
│                                                  │
│  ── (실행 중일 때만) ──                            │
│  ████████████░░░░░░  75%  Uploading...           │
│  [■  Cancel]                                     │
│                                                  │
└──────────────────────────────────────────────────┘
```

### Version Publish 실행 로직

**Version JSON 경로**: Settings 탭의 `BuildAutomationSettings`에 저장. Release 탭에서는 읽기 전용 표시.

**플랫폼별 독립 Publish**:
- Last Published: JSON에서 읽은 현재 저장값 (readonly). `RefreshLastVersions()`로 갱신.
- Current Version / Min Version: 편집 가능 TextField (`_editVersionAOS`/`_editVersionIOS`)
- **Publish 버튼**: 항상 활성화 (실행 중이거나 JSON 경로 미설정 시만 비활성). 플랫폼 readiness와 무관.
- 각 플랫폼 Publish 버튼 → `RunVersionPublish(platform)`:
  1. editInfo vs lastInfo 비교하여 변경 여부 판정
  2. `UpdateVersionJson(path, VersionCheckConfig)` — currentVersion, minVersion 업데이트
  3. 변경 없으면 → commit 스킵, 로그만 출력
  4. 변경 있으면 → `GitRunner.Commit()` — git add → commit (push 하지 않음)
  5. commit message: title + body (변경 전후 currentVersion, minVersion 기록)

### Symbol Upload 실행 로직

**Upload Symbols 버튼** (`RunSymbolUploadOnly()`):
1. 심볼 경로 필드가 비어있으면 에러 로그
2. 경로가 지정되어 있으면 `SymbolUploader.UploadAndroid/UploadIOS`에 절대 경로 전달

### 심볼 경로 관리

- `_androidSymbolPath` / `_iosSymbolPath`: EditorWindow 런타임 상태
- 빌드 성공 후 `AutoFillSymbolPaths()`가 `buildOutputDir`에서 자동 탐색
- `[...]` 버튼으로 파일/폴더 수동 선택 가능 (Android: 파일, iOS: 폴더)

---

## Tab 4: Log

실시간 빌드/릴리스 로그 표시. 자체 ScrollView 사용 (메인 ScrollView와 별도).

```
┌─ Log ────────────────────────────────────────────┐
│  [Clear]  [Copy All]     Filter: [__________]    │
│                                  ☑ Auto-scroll   │
│                                                  │
│  [15:30:01] [INFO] === Build started ===         │
│  [15:30:02] [INFO] Android Build...              │
│  [15:30:45] [INFO] Build succeeded. 45.2 MB      │
│  [15:30:46] [INFO] symbol path auto-filled: ...  │
│  [15:31:00] [INFO] === Publish Version ===       │
│  [15:31:02] [INFO] git commit done               │
│                                                  │
└──────────────────────────────────────────────────┘
```

---

## IMGUI 스냅샷 패턴

IMGUI는 Layout → Repaint 두 패스를 실행한다.
async continuation이 두 패스 사이에 상태를 변경하면 컨트롤 수 불일치로 `EndLayoutGroup` 에러가 발생한다.

**해결**: Layout 패스에서 컨트롤 수에 영향을 주는 모든 상태를 스냅샷으로 캡처하고, OnGUI 전체에서 스냅샷만 사용한다.

```csharp
// Layout 패스에서 캡처
if (Event.current.type == EventType.Layout)
{
    _guiIsRunning = _isRunning;
    _guiPhaseLabel = _currentPhaseLabel;
    _guiProgress = _progress;
    _guiTab = _currentTab;
    _guiPrereqStatus = _prereqStatus;
    _guiVersionDirty = _versionDirty;
    _guiHasAppComponent = _appComponent != null;
    _guiDevBuild = _settings != null && _settings.developmentBuild;
    _guiAppBundle = EditorUserBuildSettings.buildAppBundle;
    _guiAndroidReady = _androidReady;
    _guiIosReady = _iosReady;
    _guiBuildAndroid = _buildAndroid;
    _guiBuildIos = _buildIos;
    _guiLastVersionAOS = _lastVersionAOS;
    _guiLastVersionIOS = _lastVersionIOS;
    _guiEditVersionAOS = _editVersionAOS;
    _guiEditVersionIOS = _editVersionIOS;
}
```

조건부 컨트롤은 항상 동일한 수를 그린다 (else 분기에서 빈 컨트롤 사용).

---

## BuildAutomationLogger

스레드 안전한 중앙 로그 시스템.

- 타입: `BuildLogLevel` (Info/Warning/Error), `BuildLogEntry`
- 메인 스레드: `_entries`에 직접 추가
- 백그라운드 스레드: `_pendingEntries` 큐에 추가 → `FlushPendingEvents()`에서 메인 스레드로 이동
- EditorWindow는 `EditorApplication.update`에서 `FlushPendingEvents()` 호출 후, 로그 수 변경 시 `Repaint()`
- 로그 스냅샷: Layout 패스에서 `_logSnapshot` 배열로 복사하여 Repaint 중 안정성 보장

---

## 파일 목록

```
Samples~/MobilePackage/Editor/Build/
├── BuildAutomationSettings.cs         # ScriptableObject (10-settings)
├── BuildAutomationSettingsEditor.cs   # Custom Editor (helpBox GroupBox, 경로 브라우저)
├── BuildAutomationUtil.cs             # 공통 유틸 (Settings 로드, CLI 해석, Prerequisites)
├── BuildAutomationLogger.cs           # 로그 시스템 (스레드 안전)
├── BuildAutomationWindow.cs           # EditorWindow 본체 (4탭)
├── AndroidBuildRunner.cs              # Build - Android
├── IOSBuildRunner.cs                  # Build - iOS
├── BuildReportAnalyzer.cs             # Build - 리포트 분석
├── GitRunner.cs                       # Release - git CLI wrapper
└── SymbolUploader.cs                  # Release - Symbol Upload
```

---

## Related

- [00-overview](../00-overview/SKILL.md) — 그룹 개요
- [10-settings](../10-settings/SKILL.md) — BuildAutomationSettings 정의
- [20-build](../20-build/SKILL.md) — Build 로직
- [30-symbol-upload](../30-symbol-upload/SKILL.md) — Symbol Upload
- [40-version-publish](../40-version-publish/SKILL.md) — Version Publish
