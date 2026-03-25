# 23-ui-package

Status: ACTIVE
AppliesTo: v11
Type: Index / Directory

## Purpose

`com.devian.foundation/Samples~/UIPackage` 샘플의 UI 관련 컴포넌트/규약 인덱스 문서이다.

---

## Code Location

UI 컴포넌트 코드는 `com.devian.foundation` 내 샘플 패키지로 제공된다:

```
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/
```

### Implementation Location (3-path mirror)

| 경로 | 역할 |
|------|------|
| `framework-cs/upm/com.devian.foundation/Samples~/UIPackage/` | UPM (정본) |
| `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/UIPackage/` | Packages (sync) |
| `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/{version}/UIPackage/` | Assets/Samples (import) |

주요 파일: `Base/UIManager.cs`, `Base/UIBaseCanvas.cs`, `Base/UIBasePanel.cs`, `Base/UIBaseContainer.cs`, `Base/UIBaseFrame.cs`, `Base/UIUtils.cs`, `Component/*` 등.

> **이전 위치:** `com.devian.foundation/Runtime/Unity/UI/` → `com.devian.ui/Runtime/` → `com.devian.foundation/Samples~/UIPackage/` 로 이관 완료

---

## Sample Dependencies (UIPackage)

UIPackage는 `com.devian.foundation` 내 샘플이므로 자체 `package.json`이 없다. 의존성은 asmdef references로 관리된다.

> **Devian Domain Sound**는 `UIComponentButton`의 UI 사운드 재생에서 사용한다 (`SOUND_ID`, `TB_SOUND`, `SoundManager`, `SoundChannelType.Ui`).
> **Devian Domain Common**은 `UIComponentText`의 `ST_TEXT`/`TEXT_ID` 조회에 사용한다.

### Circular Dependency Prevention

`com.devian.foundation`은 UIPackage를 참조하지 않는다 (순환 의존 방지).

> **UIManager는 AutoSingleton**이다. `Instance` 접근 시 script code가 생성하며, scene/prefab에 미리 부착하지 않는다.

### Assembly Definitions

| asmdef | name | references | 위치 |
|--------|------|------------|------|
| `Devian.Samples.UIPackage.asmdef` | `Devian.Samples.UIPackage` | `["Devian.Core", "Devian.Samples.CommonPackage", "Devian.Samples.SoundPackage", "Unity.TextMeshPro"]` | `Runtime/` |
| `Devian.Samples.UIPackage.Editor.asmdef` | `Devian.Samples.UIPackage.Editor` | `["Devian.Samples.UIPackage", "Devian.Samples.CommonPackage", "Devian.Unity.Editor"]` | `Editor/` |

---

## Components

| ID | 컴포넌트 | 설명 | 스킬 |
|----|----------|------|------|
| 00 | Overview | 진입점/범위 | `00-overview/SKILL.md` |
| 01 | Policy | 문서 작성 정책 (Usage 섹션 금지 등) | `01-policy/SKILL.md` |
| 10 | BaseSystem | UIBaseCanvas/UIBasePanel/UIManager/UISettings 기반 시스템 그룹 | `10-base-system/00-overview/SKILL.md` |
| 11 | UILoadingSystem | 전역 blocking loading canvas / spinner / bundle loading / scene loading | `11-ui-loading-system/00-overview/SKILL.md` |
| 12 | UIPopupSystem | stack 기반 modal popup manager / canvas / panel / frame | `12-ui-popup-system/00-overview/SKILL.md` |
| 13 | UIToastSystem | overlay toast service / canvas / panel / group / frame | `13-ui-toast-system/00-overview/SKILL.md` |
| 20 | UIComponents | 재사용 UI 컴포넌트 그룹 (Button, Text, CircleFilter, NonDrawing) | `20-ui-components/00-overview/SKILL.md` |
| 21 | UIScrollSystem | scroll owner / section frame / scroll cell ID 그룹 | `21-ui-scroll-system/00-overview/SKILL.md` |
| 22 | UITweenSystem | UI 전용 최소 tween / transition 그룹 | `22-ui-tween-system/00-overview/SKILL.md` |

---

## Reference

- Parent: `skills/devian/10-module/03-ssot/SKILL.md` (Foundation Package SSOT)
- Related: `skills/devian-unity/02-unity-bundles/SKILL.md` (번들 구성/의존 정책)
- Related: `skills/devian-unity/10-foundation/SKILL.md` (기타 Unity 컴포넌트)
