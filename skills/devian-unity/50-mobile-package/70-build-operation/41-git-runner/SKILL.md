# 41-git-runner

Status: ACTIVE
AppliesTo: v1
Type: Component Specification

## 목적

git CLI를 `System.Diagnostics.Process`로 호출하는 static 래퍼.
Version Publish(Release 탭)에서 사용한다.

## 파일 위치 (SSOT)

- `com.devian.foundation/Samples~/MobilePackage/Editor/BuildAutomation/GitRunner.cs`

## Public API

```csharp
namespace Devian
{
    public static class GitRunner
    {
        /// <param name="commitMessage">커밋 메시지</param>
        /// <param name="filePaths">스테이징할 파일 경로 배열 (workingDirectory 기준 상대경로)</param>
        /// <param name="ct">취소 토큰</param>
        /// <param name="workingDirectory">git 실행 디렉토리. null이면 Unity 프로젝트 루트 사용</param>
        public static Task<bool> Commit(
            string commitMessage,
            string[] filePaths,
            CancellationToken ct = default,
            string workingDirectory = null);

        /// 현재 실행 중인 프로세스를 강제 종료한다.
        public static void Kill();
    }
}
```

## Commit 동작

```
1. workingDirectory 결정
   - 지정 시 → 해당 디렉토리 사용 (외부 repo 지원)
   - null 시  → Unity 프로젝트 루트 (Application.dataPath/..)
2. git add {filePaths}  (cwd: workingDirectory)
3. git commit -m "{commitMessage}"
4. 각 단계에서 exit code 검증, 실패 시 로그 + 중단
```

## 내부 구조

### RunGit

```csharp
private static async Task<bool> RunGit(
    string arguments, string workingDirectory, CancellationToken ct)
```

- `BuildAutomationUtil.CreateStartInfo`로 `ProcessStartInfo` 생성
- `BuildAutomationLogger.StreamProcess`로 stdout/stderr 실시간 로그
- `CancellationToken` + 타임아웃(2분) 결합: `CancellationTokenSource.CreateLinkedTokenSource`
- 취소/타임아웃 시 `process.Kill()` 후 로그

### ResolveGitPath

macOS 기준 순회 탐색:

```
/usr/bin/git
/usr/local/bin/git
/opt/homebrew/bin/git
```

## Hard Rules

- **push 금지** — commit만 수행한다. push는 사용자가 직접 수행.
- git 인증은 사용자 머신의 기존 credential(SSH key, credential helper)을 사용한다.
- 로그는 `BuildAutomationLogger` 경유.
- `_currentProcess` 정적 필드로 실행 중 프로세스 추적 → `Kill()`로 강제 종료 가능.

## Implementation Location (3-path mirror)

| 경로 | 역할 |
|------|------|
| `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Editor/BuildAutomation/GitRunner.cs` | UPM (정본) |
| `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Editor/BuildAutomation/GitRunner.cs` | Packages (sync) |
| `Assets/Samples/Devian Foundation/{version}/MobilePackage/Editor/BuildAutomation/GitRunner.cs` | Assets/Samples (import) |

## Reference

- Parent: `skills/devian-unity/50-mobile-package/70-build-operation/00-overview/SKILL.md`
- Version Publish: `skills/devian-unity/50-mobile-package/70-build-operation/40-version-publish/SKILL.md`
- BuildAutomationUtil: `skills/devian-unity/50-mobile-package/70-build-operation/50-editor-window/SKILL.md`
