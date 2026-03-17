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
        [Header("== General ==")]
        public string buildOutputDir = "Builds";

        [Header("== Android ==")]
        public bool androidEnabled = true;
        public bool buildAppBundle = true;
        public bool includeARMv7 = false;
        public string keystorePath = "";
        public string firebaseAndroidAppId = "";

        [Header("== iOS ==")]
        public bool iosEnabled = true;
        public bool autoArchive = false;
        public string firebaseIOSAppId = "";

        [Header("== CLI Paths ==")]
        public string firebaseCLIPath = "";

        [Header("== Pipeline Options ==")]
        public bool autoSymbolUpload = false;
    }
}
```

### Custom Editor

`BuildAutomationSettingsEditor`가 파일/폴더 경로 필드에 `[...]` 브라우저 버튼을 제공한다.
CLI Paths 섹션에는 자동 탐색 안내 HelpBox를 표시한다.

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
| `google-services.json` | `Assets/StreamingAssets/` 또는 프로젝트 루트 | Build (Android) |
| `GoogleService-Info.plist` | FindAssets 또는 파일 존재 | Build (iOS) |
| Firebase CLI | `ResolveFirebaseCLI()` 성공 | Symbol Upload |
| Build Output Dir | 비어있지 않음 | Build |

### PrerequisiteStatus

```csharp
public struct PrerequisiteStatus
{
    public bool firebaseSdkFound;
    public bool googleServicesJsonFound;
    public bool googleServiceInfoPlistFound;
    public bool firebaseCLIAvailable;
    public string firebaseCLIResolvedPath;
    public bool buildOutputDirValid;

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
