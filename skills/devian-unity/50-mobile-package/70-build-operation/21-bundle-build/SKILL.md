# 21-bundle-build — Addressable Bundle Build

Status: ACTIVE
AppliesTo: v1
Type: Component Specification

## 목적

Addressables 번들을 빌드한다. 특정 group을 제외하는 기능을 포함.
Pipeline 탭의 `Build Addressable Bundles` 체크박스가 활성화되어 있을 때 앱 빌드 전에 실행한다.

## 파일 위치 (SSOT)

- `com.devian.foundation/Samples~/MobilePackage/Editor/BuildAutomation/BundleBuildRunner.cs`

## Public API

```csharp
namespace Devian
{
    public static class BundleBuildRunner
    {
        /// <summary>
        /// Addressables 번들을 빌드한다.
        /// excludedGroupNames에 포함된 group은 빌드에서 제외한다.
        /// </summary>
        /// <param name="excludedGroupNames">제외할 group 이름 목록</param>
        /// <returns>성공 시 빌드 산출물 경로, 실패 시 null</returns>
        public static string Run(List<string> excludedGroupNames);
    }
}
```

## 실행 흐름

```
1. AddressableAssetSettingsDefaultObject.Settings 로드
2. excludedGroupNames에 해당하는 group의 BundledAssetGroupSchema.IncludeInBuild = false 설정
   - 변경 전 원본 값을 Dictionary<group, bool>에 백업
3. AddressableAssetSettings.BuildPlayerContent() 호출
4. 백업에서 원본 IncludeInBuild 값 복원
5. 빌드 결과 로그 출력
```

## Group 제외 메커니즘

```csharp
// 제외 처리 (빌드 전)
var backup = new Dictionary<AddressableAssetGroup, bool>();
foreach (var group in settings.groups)
{
    if (excludedGroupNames.Contains(group.Name))
    {
        var schema = group.GetSchema<BundledAssetGroupSchema>();
        if (schema != null)
        {
            backup[group] = schema.IncludeInBuild;
            schema.IncludeInBuild = false;
        }
    }
}

// 빌드 실행
AddressableAssetSettings.BuildPlayerContent();

// 복원 (빌드 후, 반드시 finally에서 실행)
foreach (var kv in backup)
{
    var schema = kv.Key.GetSchema<BundledAssetGroupSchema>();
    if (schema != null)
        schema.IncludeInBuild = kv.Value;
}
```

## 제외 group 설정

`BuildAutomationSettings.excludedAddressableGroups`에 group 이름 목록으로 저장한다.
EditorWindow Settings 탭의 "Exclude Addressable Groups" 드롭리스트에서 선택.

## EditorWindow 연동

Pipeline 탭에서 `☑ Build Addressable Bundles` 체크박스로 활성화.
앱 빌드(`RunBuild`) 실행 시 체크되어 있으면 `BundleBuildRunner.Run()` 먼저 호출.

## Hard Rules

- 제외 처리는 `IncludeInBuild` 플래그만 사용한다. group 삭제/이동하지 않는다.
- 빌드 후 반드시 원본 값을 복원한다 (try-finally).
- `AddressableAssetSettingsDefaultObject.Settings`가 null이면 에러 로그 후 중단.
- 로그는 `BuildAutomationLogger` 경유.

## Implementation Location (3-path mirror)

| 경로 | 역할 |
|------|------|
| `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Editor/BuildAutomation/BundleBuildRunner.cs` | UPM (정본) |
| `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Editor/BuildAutomation/BundleBuildRunner.cs` | Packages (sync) |
| `Assets/Samples/Devian Foundation/{version}/MobilePackage/Editor/BuildAutomation/BundleBuildRunner.cs` | Assets/Samples (import) |

## Reference

- Parent: `skills/devian-unity/50-mobile-package/70-build-operation/00-overview/SKILL.md`
- Settings: `skills/devian-unity/50-mobile-package/70-build-operation/10-settings/SKILL.md`
- EditorWindow: `skills/devian-unity/50-mobile-package/70-build-operation/50-editor-window/SKILL.md`
