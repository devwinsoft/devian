# 53-ui-popup-system — Overview

Status: ACTIVE
AppliesTo: v1

`UIPackage`의 전역 stack 기반 modal popup 시스템이다.
구조는 `UIPopupManager -> UIPopupCanvas -> UIPopupPanel -> UIPopupFrame`이며,
dim / blocker / back / duplicate / result callback / transition을 포함한다.

---

## Start Here

| Document | Description |
|----------|-------------|
| [01-policy](../01-policy/SKILL.md) | popup 모듈 경계와 hard rule |
| [10-manager](../10-manager/SKILL.md) | `UIPopupManager` — stack owner / open-close 진입점 |
| [11-ui-popup-canvas-panel](../11-ui-popup-canvas-panel/SKILL.md) | `UIPopupCanvas` + `UIPopupPanel` |
| [12-ui-popup-stack](../12-ui-popup-stack/SKILL.md) | stack / duplicate / dim / close 규칙 |
| [13-ui-popup-frame](../13-ui-popup-frame/SKILL.md) | `UIPopupFrame` state machine / transition / result |
| [14-ui-popup-data-model](../14-ui-popup-data-model/SKILL.md) | request / config / result / enum |
| [15-ui-popup-frame-id](../15-ui-popup-frame-id/SKILL.md) | `UI_POPUP_FRAME_ID` |
| [13-ui-settings](../../13-ui-settings/SKILL.md) | `UISettings` — Toast/Popup 통합 설정 asset |
| [17-ui-popup-canvas-id](../17-ui-popup-canvas-id/SKILL.md) | `UI_POPUP_CANVAS_ID` |
| [18-ui-popup-dim](../18-ui-popup-dim/SKILL.md) | `UIPopupDim` shared dim / blocker / click layer |

---

## Scope

### Includes
- `UIPopupManager.Initialize()` 기반 popup canvas bootstrap
- popup stack
- open / close top / close all
- dim + input blocking
- back / escape 처리
- duplicate policy
- callback 기반 결과 반환
- `UITransitionPlayer` 기반 open / close transition

### Excludes
- public `CloseById`
- popup pooling 정책 확장
- modal priority / multi-stack
- popup 내부 business logic

---

## Code Path

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Popup/
```

Bootstrap: `UIPopupManager.Initialize()` — `MobileApplication.onLoadCompletedAsync()`에서 호출.
