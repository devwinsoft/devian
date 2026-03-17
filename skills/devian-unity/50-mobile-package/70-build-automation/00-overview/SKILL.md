# 70-build-automation — Overview

Status: ACTIVE
AppliesTo: v11

MobilePackage 전용 모바일 빌드 자동화 스킬 그룹.
Unity EditorWindow GUI를 통해 설정 편집, 빌드 실행, 심볼 업로드, 로그 확인을 통합 제공한다.

---

## Pipeline

Build와 Symbol Upload는 독립 섹션으로 구성된다:

```
Build ──→ Symbol Upload
(독립)     (독립, 빌드 산출물 필요)
```

- **Build**: 활성화된 플랫폼(Android/iOS)을 빌드. 성공 시 심볼 경로 자동 입력.
  `autoSymbolUpload == true`이면 빌드 완료 후 자동으로 Symbol Upload 진행.
- **Symbol Upload**: Firebase Crashlytics에 심볼 업로드. 빌드 없이 독립 실행 가능 (경로 수동 지정).

---

## Sub-skills

| # | Skill | 역할 |
|---|-------|------|
| 02 | [02-install](02-install/SKILL.md) | 사전 도구 설치 (Firebase SDK/CLI, Node.js) |
| 10 | [10-settings](10-settings/SKILL.md) | 공통 설정 (Firebase App ID, 빌드 경로, CLI 경로 등) |
| 20 | [20-build](20-build/SKILL.md) | Build: Unity 빌드 (Android/iOS 플랫폼 분기) |
| 30 | [30-symbol-upload](30-symbol-upload/SKILL.md) | Symbol Upload: Firebase Crashlytics 심볼 업로드 |
| 50 | [50-editor-window](50-editor-window/SKILL.md) | Unity EditorWindow GUI (설정 편집 + 실행 + 로그) |

---

## Implementation Location (3-path mirror)

| 경로 | 역할 |
|------|------|
| `framework-cs/upm/com.devian.foundation/Samples~/MobilePackage/Editor/Build/` | UPM (정본) |
| `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/MobilePackage/Editor/Build/` | Packages (sync) |
| `Assets/Samples/Devian Foundation/{version}/Mobile Package/Editor/Build/` | Assets/Samples (import) |

---

## Related

- [devian-unity/01-policy](../../../devian-unity/01-policy/SKILL.md) — UPM Sync/미러 정책
- [50-mobile-package/01-policy](../../01-policy/SKILL.md) — MobilePackage 정책
