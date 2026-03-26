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
Samples~/MobilePackage/Editor/BuildAutomation/BuildAutomationSettings.cs
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

        // ── Release ──
        public string releaseRepoRoot = "";
        public string remoteCdnUrl = "";   // CDN base URL (예: https://xxx.cloudfront.net)
        public string versionJsonPathAOS = "release/version_aos.json";
        public string versionJsonPathIOS = "release/version_ios.json";

        // ── Addressables ──
        public List<string> excludedAddressableGroups = new List<string>();

        // ── CLI Paths ──
        public string firebaseCLIPath = "";

        // ── Pipeline Options ──
        public bool developmentBuild = false;

        // ── Keystore Credentials (EditorPrefs) ──
        // static 프로퍼티. ScriptableObject 필드가 아닌 EditorPrefs에 저장.
        public static string KeystorePass { get; set; }   // EditorPrefs
        public static string KeyaliasName { get; set; }   // EditorPrefs
        public static string KeyaliasPass { get; set; }   // EditorPrefs
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

섹션 구성: General, Android (+ Keystore Credentials), iOS, Release, Addressables, CLI Paths, Pipeline Options

### Keystore Credentials (EditorPrefs)

Keystore 비밀번호는 ScriptableObject 필드가 아닌 **EditorPrefs**에 저장한다.

| 항목 | EditorPrefs Key | 용도 |
|------|----------------|------|
| Keystore Pass | `Devian.BuildAutomation.keystorePass` | keystore 비밀번호 |
| Key Alias Name | `Devian.BuildAutomation.keyaliasName` | key alias 이름 |
| Key Alias Pass | `Devian.BuildAutomation.keyaliasPass` | key alias 비밀번호 |

**설계 근거:**
- **git 미포함**: EditorPrefs는 macOS Preferences에 저장되어 git에 들어가지 않는다.
- **재시작 유지**: Unity 재시작 후에도 값이 유지된다 (SessionState와 다름).
- **환경변수 fallback**: EditorPrefs 값이 비어있으면 환경변수(`ANDROID_KEYSTORE_PASS` 등)를 사용한다.
- **Inspector UI**: Settings Inspector의 Android 섹션에 PasswordField로 표시 (마스킹).

### Remote CDN URL

`remoteCdnUrl` 필드는 번들 업로드 경로와 Addressables Remote.LoadPath의 **SSOT(단일 진실 공급원)**이다.

| 사용처 | 경로 생성 규칙 |
|--------|---------------|
| BundleUploadRunner dest | `{releaseRepoRoot}/v{bundleVersion}/{BuildTarget}/` |
| Addressables Remote.LoadPath | `{remoteCdnUrl}/v{bundleVersion}/{BuildTarget}` |
| git commit 대상 | `v{bundleVersion}/{BuildTarget}` |

- `remoteCdnUrl`이 비어있으면 BundleUpload 시 경고 로그 후 skip.
- BundleBuildRunner가 빌드 전에 Addressables Profile의 `Remote.LoadPath`를 `{remoteCdnUrl}/v{bundleVersion}/[BuildTarget]`로 자동 동기화한다.

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
| Addressables | `com.unity.addressables` UPM 패키지 존재 | Bundle Build, Bundle Upload |

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
