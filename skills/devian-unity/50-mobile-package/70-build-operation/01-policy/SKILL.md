# 70-build-operation — Policy

Status: ACTIVE
AppliesTo: v1

---

## 1. Pipeline 빌드 테스트 정책

### 1.1 Addressables NRE 진단 체크리스트

`BuildPlayerContent`에서 `Object reference not set to an instance of an object` (duration: 0:00:00) 발생 시, 아래 순서로 진단한다.

#### Step 1: Domain Reload 확인

- **직전에 C# 파일을 수정했는가?** (스크립트 재컴파일 → domain reload)
- Unity 6000.x 베타에서 domain reload 후 Addressables 내부 정적 캐시가 깨지는 버그가 존재한다.
- **해결**: Unity 에디터 재시작 후 재시도.

#### Step 2: Roslyn (script-execute) 빌드 테스트

domain reload 무관하게 실패하면, MCP `script-execute`로 동일 빌드를 실행한다:

```csharp
// Roslyn 진단 빌드 — 이것이 성공하면 domain reload 문제
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;

public class DiagBuild
{
    public static object Main()
    {
        AddressablesPlayerBuildResult result;
        AddressableAssetSettings.BuildPlayerContent(out result);
        if (result == null) return "NULL";
        if (!string.IsNullOrEmpty(result.Error)) return $"FAIL: {result.Error}";
        return $"OK: {result.OutputPath}";
    }
}
```

| Roslyn 결과 | 원인 | 조치 |
|-------------|------|------|
| 성공 | domain reload로 인한 Addressables 내부 상태 깨짐 | Unity 재시작 |
| 실패 | Addressables 설정 자체 문제 | Step 3으로 |

#### Step 3: Settings Diagnostic Dump

Roslyn으로 Settings 상태를 덤프하여 null group, 잘못된 profile, 누락된 schema를 확인:

- `AddressableAssetSettingsDefaultObject.Settings` null 여부
- `activeProfileId` 유효성
- `ActivePlayerDataBuilder` 설정 여부
- 각 group의 schemas, entries에서 null 존재 여부
- 각 `BundledAssetGroupSchema`의 `BuildPath`/`LoadPath` 접근 시 예외 여부

#### Step 4: 디스크 상태 확인

- `Assets/AddressableAssetsData/Android/addressables_content_state.bin` 존재/크기
- `Library/com.unity.addressables/` 디렉토리 존재
- `ServerData/{BuildTarget}/` 디렉토리 내용

---

### 1.2 금지 사항 (Hard Rule)

빌드 전 캐시 조작으로 NRE를 유발한 이력이 있으므로, 아래 행위를 금지한다:

| 금지 행위 | 이유 |
|-----------|------|
| `Library/com.unity.addressables/` 수동 삭제 | Addressables 메모리 캐시와 디스크 불일치 → NRE |
| `ServerData/{BuildTarget}/` 빌드 전 삭제 | Remote group 빌드 시 내부 참조 실패 → NRE |
| `addressables_content_state.bin` 빌드 전 삭제 | incremental build 상태 깨짐 가능 |
| `AddressableAssetSettings.CleanPlayerContent()` 빌드 직전 호출 | 메모리-디스크 불일치 유발 |

**허용되는 유일한 캐시 정리 방법**: Unity 에디터 재시작.

---

### 1.3 Pipeline 빌드 실행 컨텍스트

`BuildAutomationWindow.RunBuild()`는 `async void`이며, OnGUI Button 클릭에서 동기적으로 시작된다.

```
OnGUI → Button Click → RunBuild() → BundleBuildRunner.Run() (동기)
                                   → await BundleUploadRunner.Run() (비동기)
                                   → AndroidBuildRunner / IOSBuildRunner (동기)
```

- **OnGUI에서 빌드 직접 호출 금지**: `EditorApplication.delayCall`로 defer해야 한다.
  - OnGUI 내에서 `BuildPipeline.BuildPlayer` 호출 시 GUI 레이아웃 에러 발생.
  - `UnitySynchronizationContext` (player loop 내)에서 호출 시 "A player build cannot be executed while inside the player loop" 에러.
- **`EditorApplication.delayCall` 패턴**: 모든 Build/Upload 버튼 클릭은 `EditorApplication.delayCall += () => Run...()` 형태로 호출한다.
- **`await Task.Run()` 금지**: context switch로 인해 이후 코드가 player loop 안에서 실행됨. `GitRunner`는 `process.WaitForExit()` (동기) + `Task.FromResult` 사용.
- **`Task.Yield()` 금지**: player loop context에서 continuation이 실행되어 동일 문제 발생.
- NRE 발생 시 실행 컨텍스트가 아닌 **domain reload / Addressables 내부 상태**를 먼저 의심한다.

---

## 2. Remote Bundle 경로 정책 (SSOT: remoteCdnUrl)

### 2.0 경로 매칭 규칙

Remote 번들의 업로드 경로와 런타임 다운로드 경로는 **동일 규칙**으로 매칭되어야 한다.

| 항목 | 경로 규칙 |
|------|-----------|
| Addressables Remote.LoadPath | `{remoteCdnUrl}/v{bundleVersion}/[BuildTarget]` |
| BundleUploadRunner dest | `{releaseRepoRoot}/v{bundleVersion}/{BuildTarget}/` |
| git commit 대상 | `v{bundleVersion}/{BuildTarget}` |

- **SSOT**: `BuildAutomationSettings.remoteCdnUrl` 필드 하나로 CDN URL을 관리한다.
- **자동 동기화**: BundleBuildRunner가 빌드 전에 Addressables Profile의 `Remote.LoadPath`를 Settings의 `remoteCdnUrl` 기반으로 자동 설정한다.
- **버전 분리**: `v{bundleVersion}/` prefix로 버전별 번들을 분리 저장한다. 이전 버전 번들은 삭제하지 않는다.

---

## 3. Bundle Upload 정책

### 3.1 복사 대상

- `Build & Load Paths`가 **Remote**인 group의 빌드 산출물만 복사한다.
- Remote 여부 판별: `LoadPath` resolved 값이 `http://` 또는 `https://`로 시작.
- 복사 원본: `ServerData/{BuildTarget}/` (Remote group의 BuildPath resolved 값)

### 3.2 Stale 파일 처리

- dest 디렉토리를 **clean 후 전체 복사**한다. 타임스탬프 기반 필터링 금지.
- Addressables incremental build는 변경되지 않은 번들을 재생성하지 않으므로, 타임스탬프 필터는 유효한 파일을 누락시킨다.

### 3.3 Git Commit

- `nothing to commit` (exit code 1)은 **warning** 처리. 빌드 파이프라인을 중단하지 않는다.
- 실제 git 에러만 **error** 처리.

---

## Related

- Parent: [00-overview](../00-overview/SKILL.md)
- [50-mobile-package/01-policy](../../01-policy/SKILL.md)
- [21-bundle-build](../21-bundle-build/SKILL.md)
- [22-bundle-upload](../22-bundle-upload/SKILL.md)
