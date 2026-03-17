# 50-editor-window — Build Automation GUI

Status: ACTIVE
AppliesTo: v11

Unity EditorWindow로 구현하는 빌드 자동화 GUI.
설정 편집, 빌드 실행, 심볼 업로드, 실시간 로그를 하나의 창에서 제공한다.

---

## Window 구조

EditorWindow는 3개 탭으로 구성한다:

```
┌──────────────────────────────────────────────────┐
│  [Settings]  [Pipeline]  [Log]     ● Building... │
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

```
┌─ Settings ───────────────────────────────────────┐
│                                                  │
│  ── General ──                                   │
│  Build Output Dir    [Builds          ]  [...]   │
│                                                  │
│  ── Android ──                                   │
│  ☑ Android Enabled                               │
│  ☑ Build App Bundle (AAB)                        │
│  ☐ Include ARMv7                                 │
│  Keystore Path       [                ]  [...]   │
│  Firebase App ID     [1:xxx:android:xx]          │
│                                                  │
│  ── iOS ──                                       │
│  ☑ iOS Enabled                                   │
│  ☐ Auto Archive (xcodebuild)                     │
│  Firebase App ID     [1:xxx:ios:xxxxx]           │
│                                                  │
│  ── CLI Paths ──                                 │
│  Firebase CLI Path   [                ]  [...]   │
│                                                  │
│  ── Pipeline Options ──                          │
│  ☐ Auto Symbol Upload                            │
│                                                  │
│  ── Prerequisites ──                             │
│  ✅ Firebase SDK                                 │
│  ✅ google-services.json                         │
│  ✅ GoogleService-Info.plist                     │
│  ✅ Firebase CLI  → /usr/local/bin/firebase      │
│  ✅ Build Output Dir                             │
│  [Refresh]                                       │
│                                                  │
└──────────────────────────────────────────────────┘
```

Prerequisites 상태는 `OnEditorUpdate`에서 10초 간격으로 갱신한다 (OnGUI 내부 금지).

---

## Tab 2: Pipeline

3개 섹션으로 구성: Version Info / Build / Symbol Upload.
Settings/Pipeline 탭은 공용 `_mainScroll` ScrollView로 감싼다.

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
│  ☑ Android    ☑ iOS  (읽기 전용)                  │
│  [▶  Build]                                      │
│  ─────────────────────────────────────────────   │
│                                                  │
│  ── Symbol Upload ──                             │
│  Android Symbols  [/abs/path/symbols.zip] [...]  │
│  iOS dSYMs        [/abs/path/dSYMs      ] [...]  │
│  [⬆  Upload Symbols]                            │
│                                                  │
│  ── (실행 중일 때만) ──                            │
│  ████████████░░░░░░  75%  Building...            │
│  [■  Cancel]                                     │
│                                                  │
└──────────────────────────────────────────────────┘
```

### 실행 로직

**Build 버튼** (`RunBuild()`):
1. 활성화된 플랫폼(Android/iOS)을 빌드
2. 빌드 성공 시 `AutoFillSymbolPaths()`로 심볼 경로 자동 입력
3. `autoSymbolUpload == true`이면 자동으로 Symbol Upload 진행

**Upload Symbols 버튼** (`RunSymbolUploadOnly()`):
1. 심볼 경로 필드가 비어있으면 에러 로그
2. 경로가 지정되어 있으면 `SymbolUploader.UploadAndroid/UploadIOS`에 절대 경로 전달

### 심볼 경로 관리

- `_androidSymbolPath` / `_iosSymbolPath`: EditorWindow 런타임 상태 (Settings에 저장하지 않음)
- 빌드 성공 후 `AutoFillSymbolPaths()`가 `buildOutputDir`에서 자동 탐색
- `[...]` 버튼으로 파일/폴더 수동 선택 가능 (Android: 파일, iOS: 폴더)
- 텍스트 필드에 절대 경로 직접 입력 가능

---

## Tab 3: Log

실시간 빌드 로그 표시. 자체 ScrollView 사용 (메인 ScrollView와 별도).

```
┌─ Log ────────────────────────────────────────────┐
│  [Clear]  [Copy All]     Filter: [__________]    │
│                                  ☑ Auto-scroll   │
│                                                  │
│  [15:30:01] [INFO] === Build started ===         │
│  [15:30:02] [INFO] Android Build...              │
│  [15:30:45] [INFO] Build succeeded. 45.2 MB      │
│  [15:30:46] [INFO] symbol path auto-filled: ...  │
│  [15:30:47] [WARN] iOS: dSYMs not found          │
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
├── BuildAutomationSettingsEditor.cs   # Custom Editor (경로 브라우저)
├── BuildAutomationUtil.cs             # 공통 유틸 (Settings 로드, CLI 해석, Prerequisites)
├── BuildAutomationLogger.cs           # 로그 시스템 (스레드 안전)
├── BuildAutomationWindow.cs           # EditorWindow 본체
├── AndroidBuildRunner.cs              # Build - Android
├── IOSBuildRunner.cs                  # Build - iOS
├── BuildReportAnalyzer.cs             # Build - 리포트 분석
└── SymbolUploader.cs                  # Symbol Upload
```

---

## Related

- [00-overview](../00-overview/SKILL.md) — 그룹 개요
- [10-settings](../10-settings/SKILL.md) — BuildAutomationSettings 정의
- [20-build](../20-build/SKILL.md) — Build 로직
- [30-symbol-upload](../30-symbol-upload/SKILL.md) — Symbol Upload
