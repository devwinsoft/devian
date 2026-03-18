# 10-settings — Build Automation Settings

Status: ACTIVE
AppliesTo: v11

빌드 자동화 파이프라인의 공통 설정을 정의한다.
GUI(50-editor-window)와 각 Runner 스크립트가 이 설정을 참조한다.

---

## ScriptableObject: BuildAutomationSettings

`BuildAutomationSettings.asset`을 통해 프로젝트별 설정을 영속 저장한다.
EditorWindow Settings 탭에서 이 ScriptableObject를 직접 편집한다.

### 파일 위치

```
Samples~/MobilePackage/Editor/Build/BuildAutomationSettings.cs
```

### 필드 정의

```csharp
namespace Devian
{
    [CreateAssetMenu(
        fileName = "BuildAutomationSettings",
        menuName = "Devian/Build Automation Settings")]
    public class BuildAutomationSettings : ScriptableObject
    {
        // ── General ──
        public string buildOutputDir = "Builds";

        // ── Android ──
        public bool includeARMv7 = false;
        public string keystorePath = "";
        public string firebaseAndroidAppId = "";

        // ── iOS ──
        public string firebaseIOSAppId = "";

        // ── Version ──
        public string versionJsonPathAOS = "release/version_aos.json";
        public string versionJsonPathIOS = "release/version_ios.json";

        // ── CLI Paths ──
        public string firebaseCLIPath = "";

        // ── Pipeline Options ──
        public bool developmentBuild = false;
    }
}
```

> `[Header]` 속성을 사용하지 않는다. CustomEditor(`BuildAutomationSettingsEditor`)가 `EditorStyles.helpBox` GroupBox로 섹션을 시각적으로 묶는다.

> **자동감지 항목** (Settings에 포함하지 않음):
> - **플랫폼 (Android/iOS)**: `BuildPipeline.IsBuildTargetSupported()`로 빌드 모듈 설치 여부 자동감지
> - **AAB/APK**: `EditorUserBuildSettings.buildAppBundle` (Unity 자체 설정) — Pipeline 탭에서 토글

### Custom Editor

`BuildAutomationSettingsEditor`가 Inspector를 그린다.
각 섹션을 `EditorStyles.helpBox` VerticalScope로 감싸 GroupBox 형태로 묶는다.
파일/폴더 경로 필드에는 `DrawPathFieldInline()` (Label + TextField + `[...]` 브라우저 버튼)을 사용한다.
CLI Paths 섹션에는 자동 탐색 안내 문구를 `wordWrappedMiniLabel`로 표시한다.

섹션 구성: General, Android, iOS, Version JSON, CLI Paths, Pipeline Options

### 설정 로드

```csharp
// BuildAutomationUtil.LoadSettings()
var guids = AssetDatabase.FindAssets("t:BuildAutomationSettings");
var path = AssetDatabase.GUIDToAssetPath(guids[0]);
return AssetDatabase.LoadAssetAtPath<BuildAutomationSettings>(path);
```

---

## 사전 요건 (Prerequisites)

Settings 탭 하단에서 상태를 표시한다. `BuildAutomationUtil.CheckPrerequisites()`로 검사.

| 항목 | 확인 방법 | 필요 시점 |
|------|-----------|-----------|
| Firebase SDK | `Assets/Firebase/` 또는 UPM 패키지 존재 | Build, Symbol Upload |
| Firebase Android App ID | `settings.firebaseAndroidAppId` 비어있지 않음 | Build (Android) |
| Firebase iOS App ID | `settings.firebaseIOSAppId` 비어있지 않음 | Build (iOS) |
| `google-services.json` | `Assets/StreamingAssets/` 또는 프로젝트 루트 | Build (Android) |
| `GoogleService-Info.plist` | FindAssets 또는 파일 존재 | Build (iOS) |
| Firebase CLI | `ResolveFirebaseCLI()` 성공 | Symbol Upload |
| Build Output Dir | 비어있지 않음 | Build |
| Android Module | `BuildPipeline.IsBuildTargetSupported()` | Build (Android) |
| iOS Module | `BuildPipeline.IsBuildTargetSupported()` | Build (iOS) |

### PrerequisiteStatus

```csharp
public struct PrerequisiteStatus
{
    public bool firebaseSdkFound;
    public bool firebaseAndroidAppIdSet;
    public bool firebaseIOSAppIdSet;
    public bool googleServicesJsonFound;
    public bool googleServiceInfoPlistFound;
    public bool firebaseCLIAvailable;
    public string firebaseCLIResolvedPath;
    public bool buildOutputDirValid;
    public bool androidSupported;
    public bool iosSupported;

    public bool AllRequiredForBuild =>
        firebaseSdkFound && buildOutputDirValid;
    public bool AllRequiredForSymbolUpload =>
        AllRequiredForBuild && firebaseCLIAvailable;
}
```

---

## Related

- [00-overview](../00-overview/SKILL.md) — 그룹 개요
- [50-editor-window](../50-editor-window/SKILL.md) — GUI에서 설정 편집
