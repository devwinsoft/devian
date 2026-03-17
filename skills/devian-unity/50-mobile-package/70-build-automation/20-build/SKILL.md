# 20-build — Build

Status: ACTIVE
AppliesTo: v11

Unity 모바일 빌드를 실행한다. Android/iOS 플랫폼별 분기를 포함.
`BuildAutomationSettings`에서 활성화된 플랫폼만 빌드한다.

---

## 진입점

EditorWindow Pipeline 탭의 Build 섹션에서 `▶ Build` 버튼으로 실행한다.
내부적으로 `BuildAutomationWindow.RunBuild()` → `RunPhase1()`이 호출된다.

```csharp
// RunPhase1() 내부
if (settings.androidEnabled)
    AndroidBuildRunner.Run(settings);

if (settings.iosEnabled)
    IOSBuildRunner.Run(settings);
```

---

## Android Build

### AndroidBuildRunner

```csharp
namespace Devian
{
    public static class AndroidBuildRunner
    {
        public static BuildReport Run(BuildAutomationSettings settings)
        {
            EditorUserBuildSettings.buildAppBundle = settings.buildAppBundle;
            EditorUserBuildSettings.androidCreateSymbols = AndroidCreateSymbols.Debugging;
            PlayerSettings.SetScriptingBackend(
                BuildTargetGroup.Android, ScriptingBackend.IL2CPP);

            var architectures = AndroidArchitecture.ARM64;
            if (settings.includeARMv7)
                architectures |= AndroidArchitecture.ARMv7;
            PlayerSettings.Android.targetArchitectures = architectures;

            if (!string.IsNullOrEmpty(settings.keystorePath))
                PlayerSettings.Android.keystoreName = settings.keystorePath;

            var ext = settings.buildAppBundle ? "aab" : "apk";
            var outputPath = $"{settings.buildOutputDir}/Android/{Application.productName}.{ext}";

            var options = new BuildPlayerOptions
            {
                scenes = GetEnabledScenes(),
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            return BuildPipeline.BuildPlayer(options);
        }
    }
}
```

### 산출물

| 파일 | 경로 |
|------|------|
| AAB/APK | `{buildOutputDir}/Android/{productName}.aab` |
| symbols.zip | `{buildOutputDir}/Android/{productName}-{version}-v{bundleVersionCode}.symbols.zip` |

---

## iOS Build

### IOSBuildRunner

```csharp
namespace Devian
{
    public static class IOSBuildRunner
    {
        public static BuildReport Run(BuildAutomationSettings settings)
        {
            var outputPath = $"{settings.buildOutputDir}/iOS";

            var options = new BuildPlayerOptions
            {
                scenes = GetEnabledScenes(),
                locationPathName = outputPath,
                target = BuildTarget.iOS,
                options = BuildOptions.None
            };

            return BuildPipeline.BuildPlayer(options);
        }
    }
}
```

### Xcode Archive (autoArchive 설정 시)

Unity 빌드 완료 후 `autoArchive == true`이면 xcodebuild를 호출하여 `.xcarchive`와 dSYM을 생성한다.

### 산출물

| 파일 | 경로 |
|------|------|
| Xcode project | `{buildOutputDir}/iOS/` |
| Archive (autoArchive 시) | `{buildOutputDir}/iOS/app.xcarchive` |
| dSYM (autoArchive 시) | `{buildOutputDir}/iOS/app.xcarchive/dSYMs/*.dSYM` |

---

## BuildReport 처리

빌드 완료 후 `BuildReportAnalyzer.LogReport()`로 결과를 GUI 로그에 전달한다.

---

## Build 완료 후 동작

```
Build 완료
  → AutoFillSymbolPaths() (심볼 경로 자동 입력)
  ├─ autoSymbolUpload == true → Symbol Upload 자동 시작
  └─ autoSymbolUpload == false → 종료 (수동으로 Upload Symbols 클릭)
```

---

## Related

- [10-settings](../10-settings/SKILL.md) — 빌드 설정
- [30-symbol-upload](../30-symbol-upload/SKILL.md) — Symbol Upload (다음 단계)
- [50-editor-window](../50-editor-window/SKILL.md) — GUI
