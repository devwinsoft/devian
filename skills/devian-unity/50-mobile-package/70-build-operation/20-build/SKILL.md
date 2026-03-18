# 20-build — Build

Status: ACTIVE
AppliesTo: v11

Unity 모바일 빌드를 실행한다. Android/iOS 플랫폼별 분기를 포함.
사용자가 Pipeline 탭에서 선택한 플랫폼(`_buildAndroid`/`_buildIos`)만 빌드한다.

---

## 진입점

EditorWindow Pipeline 탭의 Build 섹션에서 `▶ Build` 버튼으로 실행한다.
내부적으로 `BuildAutomationWindow.RunBuild()` → `RunPhase1()`이 호출된다.

```csharp
// RunPhase1() 내부
if (_buildAndroid)
    AndroidBuildRunner.Run(settings);

if (_buildIos)
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
            // AAB/APK — EditorUserBuildSettings.buildAppBundle을 그대로 사용
            EditorUserBuildSettings.androidCreateSymbols = AndroidCreateSymbols.Debugging;
            PlayerSettings.SetScriptingBackend(
                BuildTargetGroup.Android, ScriptingBackend.IL2CPP);

            var architectures = AndroidArchitecture.ARM64;
            if (settings.includeARMv7)
                architectures |= AndroidArchitecture.ARMv7;
            PlayerSettings.Android.targetArchitectures = architectures;

            if (!string.IsNullOrEmpty(settings.keystorePath))
                PlayerSettings.Android.keystoreName = settings.keystorePath;

            var isAppBundle = EditorUserBuildSettings.buildAppBundle;
            var ext = isAppBundle ? "aab" : "apk";
            var outputPath = $"{settings.buildOutputDir}/Android/{Application.productName}.{ext}";

            var buildOptions = BuildOptions.None;
            if (settings.developmentBuild)
                buildOptions |= BuildOptions.Development | BuildOptions.AllowDebugging;

            var options = new BuildPlayerOptions
            {
                scenes = GetEnabledScenes(),
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = buildOptions
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

            var buildOptions = BuildOptions.None;
            if (settings.developmentBuild)
                buildOptions |= BuildOptions.Development | BuildOptions.AllowDebugging;

            var options = new BuildPlayerOptions
            {
                scenes = GetEnabledScenes(),
                locationPathName = outputPath,
                target = BuildTarget.iOS,
                options = buildOptions
            };

            return BuildPipeline.BuildPlayer(options);
        }
    }
}
```

### 산출물

| 파일 | 경로 |
|------|------|
| Xcode project | `{buildOutputDir}/iOS/` |

---

## BuildReport 처리

빌드 완료 후 `BuildReportAnalyzer.LogReport()`로 결과를 GUI 로그에 전달한다.

---

## Build 완료 후 동작

```
Build 완료
  → AutoFillSymbolPaths() (심볼 경로 자동 입력)
  → 종료 (Release 탭에서 수동으로 Symbol Upload / Version Publish 실행)
```

---

## Related

- [10-settings](../10-settings/SKILL.md) — 빌드 설정
- [30-symbol-upload](../30-symbol-upload/SKILL.md) — Symbol Upload (Release 탭)
- [50-editor-window](../50-editor-window/SKILL.md) — GUI
