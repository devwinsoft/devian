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

주요 파일: `UIManager.cs`, `UICanvas.cs`, `UIPanel.cs`, `UIBaseContainer.cs`, `UIBaseFrame.cs`, `UIUtils.cs`, `Component/*` 등.

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
| 10 | UIManager | Canvas 수명주기 (AutoSingleton) | `10-ui-manager/SKILL.md` |
| 11 | UICanvasSystem | UICanvas/UIPanel/UIBaseContainer 규약 | `11-ui-canvas-system/SKILL.md` |
| 12 | UI_CANVAS_ID | UICanvas prefab 참조 ID (AssetId 패턴) | `12-ui-canvas-id/SKILL.md` |
| 13 | UISettings | Toast/Popup 통합 전역 설정 asset | `13-ui-settings/SKILL.md` |
| 21 | UISimpleContainer | 최소 container 구현체 (frame subtree bootstrap) | `21-ui-container-simple/SKILL.md` |
| 22 | UIScrollContainer | 유일한 scroll owner (UIScrollContainer + IUIScrollSection) | `22-ui-container-scroll/SKILL.md` |
| 23 | UIGridFrame | N열 grid section renderer (UIGridFrame + UIGridCell) | `23-ui-frame-grid/SKILL.md` |
| 24 | UISimpleFrame | 고정 프리팹 section (배너, 헤더, 구분선) | `24-ui-frame-simple/SKILL.md` |
| 30 | UIComponentButton | Button press feedback + UI sound + events + scroll bridge | `30-ui-plugin-button/SKILL.md` |
| 31 | UIComponentCircleFilter | Collider2D 기반 Raycast filter | `31-ui-plugin-circle-filter/SKILL.md` |
| 32 | UIComponentNonDrawing | Non-drawing Graphic | `32-ui-plugin-non-drawing/SKILL.md` |
| 33 | UIMessageSystem | UI 전용 메시지 시스템 (UnityEngine.EntityId + UI_MESSAGE) | `33-ui-message-system/SKILL.md` |
| 34 | UIComponentText | ST_TEXT 바인딩 텍스트 플러그인 (InitOnce + ReloadText) | `34-ui-plugin-text/SKILL.md` |
| 40 | UI_CONTAINER_ID | UIBaseContainer 프리팹 참조 ID (AssetId 패턴) | `40-ui-container-id/SKILL.md` |
| 41 | UI_CELL_ID | UIGridCell 프리팹 참조 ID (AssetId 패턴) | `41-ui-cell-id/SKILL.md` |
| 50 | UIUtils | 공용 static 유틸리티 (좌표 변환, Billboard, Cursor) | `50-ui-utils/SKILL.md` |
| 51 | UITweenSystem | UI 전용 최소 tween / transition 그룹 | `51-ui-tween-system/00-overview/SKILL.md` |
| 52 | UIToastSystem | overlay toast service / canvas / panel / group / frame | `52-ui-toast-system/00-overview/SKILL.md` |
| 53 | UIPopupSystem | stack 기반 modal popup manager / canvas / panel / frame | `53-ui-popup-system/00-overview/SKILL.md` |

---

## Reference

- Parent: `skills/devian/10-module/03-ssot/SKILL.md` (Foundation Package SSOT)
- Related: `skills/devian-unity/02-unity-bundles/SKILL.md` (번들 구성/의존 정책)
- Related: `skills/devian-unity/10-foundation/SKILL.md` (기타 Unity 컴포넌트)
