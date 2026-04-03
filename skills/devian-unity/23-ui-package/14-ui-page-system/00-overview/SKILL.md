# 14-page-system — Overview

Status: ACTIVE
AppliesTo: v11

`UIBasePageCanvas` 기반 page 시스템 개요.
현재 구조는 page를 `main / sub / popup`으로 분리한다.

---

## Start Here

| Document | Description |
|----------|-------------|
| [10-canvas-panel](../10-canvas-panel/SKILL.md) | `UIBasePageCanvas`, `UIBasePageMain`, `UIBasePageSub`, `UIBasePagePopup` 계약 |

---

## Scope

### Includes
- `UIBasePageCanvas` — `currentMain/currentSub/currentPopup`, `ShowPage(main)` orchestration
- `UIBasePageMain` — main-to-main left/right transition
- `UIBasePageSub` — main 종속 show/hide transition
- `UIBasePagePopup` — top layer show/hide transition, canvas당 1개
- `UIBasePagePanel` — main/sub 공통 `pageIndex` + owner canvas bridge
- `UIPageState`, `UIPageTransitionDirection`
- `UIPageSubCloseReason`, `UIPagePopupCloseReason`

### Excludes
- navigation history / back stack
- popup stacking
- page business logic

---

## Current Model

- `ShowPage()`는 `UIBasePageMain`만 대상으로 동작한다.
- `UIBasePageSub.Show()`는 현재 main을 즉시 숨기고 자기 show transition을 재생한다.
- `UIBasePageSub.Hide()`는 자기 hide transition 완료 후 main을 즉시 복구한다.
- `UIBasePagePopup.Show()`는 기존 popup을 닫고 top layer로 표시된다.
- main 전환 시 기존 sub/popup은 애니메이션 없이 즉시 닫힌다.

---

## Code Path

```text
framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/UIPackage/Runtime/Page/
├── UIBasePageCanvas.cs
├── UIBasePagePanel.cs
├── UIBasePageMain.cs
├── UIBasePageSub.cs
└── UIBasePagePopup.cs
```

---

## Related

- Parent: `../SKILL.md`
- Base Canvas System: `../../10-base-system/11-ui-canvas-system/SKILL.md`
- UITween System: `../../22-ui-tween-system/00-overview/SKILL.md`
- Design Note: `/Users/maoshy/Documents/Projects/devian/docs/ui-base-page-design.md`
