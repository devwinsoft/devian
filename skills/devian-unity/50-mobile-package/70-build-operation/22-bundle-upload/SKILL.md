# 22-bundle-upload — Addressable Bundle Upload

Status: ACTIVE
AppliesTo: v1
Type: Component Specification

## 목적

Addressables에서 Remote으로 설정된 group의 빌드 산출물만 release repo에 복사하고 git commit한다.
GitHub Pages로 서빙하여 앱이 런타임에 번들을 다운로드할 수 있게 한다.

## 파일 위치 (SSOT)

- `com.devian.foundation/Samples~/MobilePackage/Editor/BuildAutomation/BundleUploadRunner.cs`

## Public API

```csharp
namespace Devian
{
    public static class BundleUploadRunner
    {
        /// <summary>
        /// Remote group의 빌드 산출물을 release repo에 복사하고 git commit한다.
        /// </summary>
        /// <param name="settings">BuildAutomationSettings</param>
        /// <param name="ct">취소 토큰</param>
        /// <returns>성공 시 true</returns>
        public static async Task<bool> Run(
            BuildAutomationSettings settings,
            CancellationToken ct = default);
    }
}
```

## 실행 흐름

```
1. AddressableAssetSettingsDefaultObject.Settings 로드
2. Remote group 필터링
   - groups 순회 → IsRemoteGroup()으로 Remote group 탐색
3. 빌드 산출물 경로 결정
   - Remote group의 BundledAssetGroupSchema.BuildPath.GetValue(aaSettings)로 실제 빌드 경로 resolve
   - 해당 경로의 모든 파일 수집 (번들 + catalog + hash)
4. release repo 대상 경로에 복사
   - 대상: {releaseRepoRoot}/v{bundleVersion}/{BuildTarget}/
   - 기존 파일 정리 후 복사 (clean copy)
5. GitRunner.Commit()으로 git add → commit
   - workingDirectory: releaseRepoRoot
   - commitMessage: "chore: update addressable bundles (v{bundleVersion}/{BuildTarget})"
   - filePaths: v{bundleVersion}/{BuildTarget} 하위 전체
6. push는 사용자가 직접 수행
```

## Remote Group 판별

```csharp
private static bool IsRemoteGroup(AddressableAssetGroup group)
{
    var schema = group.GetSchema<BundledAssetGroupSchema>();
    if (schema == null) return false;

    // BuildPath/LoadPath가 Remote 프로파일 변수를 사용하는지 확인
    var buildPath = schema.BuildPath.GetValue(
        AddressableAssetSettingsDefaultObject.Settings);
    return buildPath != null
        && !buildPath.Contains("StreamingAssets");
}
```

## 복사 대상 파일

| 파일 유형 | 설명 |
|-----------|------|
| `*.bundle` | 번들 파일 (Remote group에 속한 것만) |
| `catalog_*.json` | 카탈로그 파일 |
| `catalog_*.hash` | 카탈로그 해시 (버전 체크용) |

## 대상 경로 구조

```
{releaseRepoRoot}/
├── v0.2.0/
│   └── Android/
│       ├── string-pb64_xxx.bundle
│       └── string-ndjson_xxx.bundle
├── v0.2.1/
│   └── Android/
│       └── ...
├── version_aos.json
└── version_ios.json
```

### 경로 매칭 규칙 (SSOT: remoteCdnUrl)

앱 런타임 요청 URL과 release repo 파일 경로가 일치해야 한다:

```
Remote.LoadPath:  {remoteCdnUrl}/v{bundleVersion}/[BuildTarget]/{bundle}
Upload dest:      {releaseRepoRoot}/v{bundleVersion}/{BuildTarget}/{bundle}
```

`remoteCdnUrl`은 `BuildAutomationSettings.remoteCdnUrl`에서 관리한다.

## EditorWindow 연동

### Release 탭 (수동 실행)

```
── Bundle Upload ──
Release Repo Root  ../release-repo
Remote Groups: {N}개 감지
[⬆  Upload Bundles to Git]
```

- Remote group 수는 `OnEditorUpdate`에서 갱신
- 버튼 클릭 → `BundleUploadRunner.Run(settings, ct)`

### Pipeline 탭 (자동 실행)

- Pipeline > Build 실행 시 `_buildAddressables`가 true이면:
  1. `BundleBuildRunner.Run()` — 번들 빌드
  2. 성공 시 `BundleUploadRunner.Run()` — 자동으로 번들 복사 + git commit
  3. Bundle Copy 실패 시 경고 로그만 출력, 앱 빌드는 계속 진행

## Hard Rules

- Remote group의 산출물만 복사한다. Local group은 제외.
- 복사 전 대상 디렉토리를 clean한다 (이전 빌드 잔여물 방지).
- push 금지 — commit만 수행. 사용자가 직접 push.
- `AddressableAssetSettingsDefaultObject.Settings`가 null이면 에러 로그 후 중단.
- `releaseRepoRoot`가 비어있으면 프로젝트 루트 사용 (`ResolveReleaseRoot()` 재활용).
- 로그는 `BuildAutomationLogger` 경유.

## Implementation Location (3-path mirror)

| 경로 | 역할 |
|------|------|
| `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Editor/BuildAutomation/BundleUploadRunner.cs` | UPM (정본) |
| `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Editor/BuildAutomation/BundleUploadRunner.cs` | Packages (sync) |
| `Assets/Samples/Devian Foundation/{version}/MobilePackage/Editor/BuildAutomation/BundleUploadRunner.cs` | Assets/Samples (import) |

## Reference

- Parent: `skills/devian-unity/50-mobile-package/70-build-operation/00-overview/SKILL.md`
- Settings: `skills/devian-unity/50-mobile-package/70-build-operation/10-settings/SKILL.md`
- GitRunner: `skills/devian-unity/50-mobile-package/70-build-operation/41-git-runner/SKILL.md`
- EditorWindow: `skills/devian-unity/50-mobile-package/70-build-operation/50-editor-window/SKILL.md`
- Install (Remote Profile): `skills/devian-unity/50-mobile-package/70-build-operation/02-install/SKILL.md`
