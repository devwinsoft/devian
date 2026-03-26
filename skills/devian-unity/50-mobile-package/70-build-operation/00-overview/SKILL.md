# 70-build-operation — Overview

Status: ACTIVE
AppliesTo: v11

MobilePackage 전용 모바일 빌드 및 릴리스 운영 스킬 그룹.
Unity EditorWindow GUI를 통해 설정 편집, 빌드 실행, 릴리스(버전 게시 + 심볼 업로드), 로그 확인을 통합 제공한다.

---

## Pipeline

Build와 Release는 독립 탭으로 구성된다:

```
Pipeline 탭: Bundle Build (Addressables) + App Build (산출물 생성)
Release 탭:  Version Publish (git) + Bundle Upload (git) + Symbol Upload (firebase CLI)
```

- **Bundle Build**: Addressables 번들 빌드. 특정 group 제외 가능.
- **App Build**: 활성화된 플랫폼(Android/iOS)을 빌드. 성공 시 심볼 경로 자동 입력.
- **Version Publish**: 버전 JSON을 플랫폼별로 업데이트하고 git commit. push는 사용자가 직접 수행.
- **Bundle Upload**: Remote group의 번들 산출물을 release repo에 복사 → git commit.
- **Symbol Upload**: Firebase Crashlytics에 심볼 업로드. 빌드 없이 독립 실행 가능.

---

## Sub-skills

| # | Skill | 역할 |
|---|-------|------|
| 01 | [01-policy](../01-policy/SKILL.md) | 빌드 테스트 정책, NRE 진단 체크리스트, 금지 사항 |
| 02 | [02-install](../02-install/SKILL.md) | 사전 도구 설치 (Firebase SDK/CLI, Node.js) |
| 10 | [10-settings](../10-settings/SKILL.md) | 공통 설정 (Firebase App ID, 빌드 경로, CLI 경로 등) |
| 20 | [20-build](../20-build/SKILL.md) | App Build: Unity 빌드 (Android/iOS 플랫폼 분기) |
| 21 | [21-bundle-build](../21-bundle-build/SKILL.md) | Bundle Build: Addressables 번들 빌드 (group 제외 기능) |
| 22 | [22-bundle-upload](../22-bundle-upload/SKILL.md) | Bundle Upload: Remote group 번들 → git commit |
| 30 | [30-symbol-upload](../30-symbol-upload/SKILL.md) | Symbol Upload: Firebase Crashlytics 심볼 업로드 |
| 40 | [40-version-publish](../40-version-publish/SKILL.md) | Version Publish: 버전 JSON → git commit (플랫폼별 독립) |
| 41 | [41-git-runner](../41-git-runner/SKILL.md) | GitRunner: git CLI 래퍼 (add → commit, Process 기반) |
| 50 | [50-editor-window](../50-editor-window/SKILL.md) | Unity EditorWindow GUI (4탭: Settings/Pipeline/Release/Log) |

---

## Implementation Location (3-path mirror)

| 경로 | 역할 |
|------|------|
| `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Editor/BuildAutomation/` | UPM (정본) |
| `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Editor/BuildAutomation/` | Packages (sync) |
| `Assets/Samples/Devian Foundation/{version}/Mobile Package/Editor/BuildAutomation/` | Assets/Samples (import) |

---

## Related

- [devian-unity/01-policy](../../../devian-unity/01-policy/SKILL.md) — UPM Sync/미러 정책
- [50-mobile-package/01-policy](../../01-policy/SKILL.md) — MobilePackage 정책
