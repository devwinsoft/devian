# 40-version-publish — Version Publish

Status: ACTIVE
AppliesTo: v11

버전 JSON 파일을 플랫폼별로 업데이트하고 git commit한다.
push는 사용자가 직접 수행한다.

---

## 대상 파일

경로는 `BuildAutomationSettings` ScriptableObject에 저장된다. Settings 탭에서 편집.

| 플랫폼 | Settings 필드 | 기본값 | JSON 내용 |
|--------|---------------|--------|-----------|
| Android | `versionJsonPathAOS` | `release/version_aos.json` | `{ "currentVersion": "x.y.z", "minVersion": "x.y.z", "update_url": "..." }` |
| iOS | `versionJsonPathIOS` | `release/version_ios.json` | `{ "currentVersion": "x.y.z", "minVersion": "x.y.z", "update_url": "..." }` |

---

## VersionCheckConfig

버전 JSON의 공통 데이터 모델. Core 레이어에 정의되어 Runtime/Editor 양쪽에서 사용한다.

→ 상세: [15-version-check-config](../../../../devian/10-module/20-core/15-version-check-config/SKILL.md)

---

## 실행 흐름

플랫폼별로 독립 실행한다. 각 플랫폼의 Publish 버튼이 개별로 동작한다.

```
1. Release 탭에서 해당 플랫폼의 currentVersion / minVersion 편집
2. [▶ Publish] 버튼 클릭
3. 변경 사항 비교 (editInfo vs lastInfo)
4. UpdateVersionJson(path, VersionCheckConfig) — JSON의 currentVersion, minVersion 업데이트
5. 변경 없으면 → "변경 사항 없음 — commit 스킵" 로그, RefreshLastVersions()
6. 변경 있으면 → git add → git commit (title + body)
```

> push는 자동으로 수행하지 않는다. 사용자가 커밋을 확인 후 직접 push한다.

### JSON 업데이트 방식

`ReplaceOrInsertJsonField()` 헬퍼를 사용하여 regex 기반으로 필드를 교체/삽입한다.
Newtonsoft 의존성 없이 동작한다.

---

## GitRunner API

git CLI를 `System.Diagnostics.Process`로 호출한다.

```csharp
namespace Devian
{
    public static class GitRunner
    {
        public static Task<bool> Commit(
            string commitMessage,
            string[] filePaths,
            CancellationToken ct)
    }
}
```

내부 동작:
1. `git add {filePaths}` — 대상 파일만 스테이징
2. `git commit -m "{commitMessage}"` — 커밋
3. 각 단계에서 exit code 검증, 실패 시 로그 + 중단

---

## EditorWindow (Release 탭)

Release 탭의 Version Publish 섹션. 플랫폼별 독립 블록으로 구성:

```
── Version Publish ──

 ── Android ──
 Last Published    current: 1.2.3  min: 1.0.0
 Current Version   [1.2.3      ]
 Min Version       [1.0.0      ]
 [▶  Publish]

 ── iOS ──
 Last Published    current: 1.2.3  min: 1.0.0
 Current Version   [1.2.3      ]
 Min Version       [1.0.0      ]
 [▶  Publish]
```

- **Last Published**: JSON 파일에서 읽은 현재 저장값 (읽기 전용)
- **Current Version / Min Version**: 편집 가능한 TextField. publish 시 이 값으로 JSON 갱신
- **Publish 버튼**: 항상 활성화 (실행 중이거나 JSON 경로 미설정 시만 비활성). 플랫폼 readiness와 무관.

---

## Hard Rules

- commit 대상은 해당 플랫폼의 version JSON 파일 1개만 한정한다. 다른 파일을 포함하지 않는다.
- **변경 사항이 없으면 commit하지 않는다.** (editInfo vs lastInfo 비교)
- commit message 형식:
  - title: `chore: bump {platform} version to {currentVersion}`
  - body: `- currentVersion: {old} → {new}` / `- minVersion: {old} → {new}`
- push는 자동으로 수행하지 않는다. 사용자가 커밋 확인 후 직접 push한다.
- git 인증은 사용자 머신의 기존 credential(SSH key, credential helper)을 사용한다.

---

## Related

- [10-settings](../10-settings/SKILL.md) — 설정 (version JSON 경로 저장)
- [50-editor-window](../50-editor-window/SKILL.md) — GUI
- [11-mobile-application](../../11-mobile-application/SKILL.md) — VersionCheck URL
