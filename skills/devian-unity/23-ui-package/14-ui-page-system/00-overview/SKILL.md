# 14-page-system — Overview

Status: ACTIVE
AppliesTo: v11

Page 전환 전용 canvas/panel specialization.
`UIBasePageCanvas`가 page host/coordinator 역할을 하고,
`UIBasePagePanel`이 개별 page의 transition/state를 담당한다.
좌우 방향 transition과 `latest wins` 동시 요청 정책을 포함한다.

---

## Start Here

| Document | Description |
|----------|-------------|
| [10-canvas-panel](../10-canvas-panel/SKILL.md) | `UIBasePageCanvas` + `UIBasePagePanel` 계약, Public API, Navigation Sequence |

---

## Scope

### Includes
- `UIBasePageCanvas` — page 수집, current page 추적, ShowPage, transition orchestration
- `UIBasePagePanel` — page identity, enter/exit transition, page state, page hook
- `UIBasePageCanvas<TCanvas>` — singleton bridge
- `UIBasePagePanel<TCanvas>` — typed `ownerCanvas` bridge
- `UIPageState` / `UIPageTransitionDirection` enum
- `UITransitionPlayer` 기반 directional transition
- `latest wins` 동시 요청 정책
- 초기 상태 정규화 (중복 active page 정리)

### Excludes
- page 내부 business logic
- page manager 서비스 (canvas가 직접 host)
- page pooling 정책 확장
- page navigation history / back stack

---

## Code Path

```
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Page/
├── UIBasePageCanvas.cs
└── UIBasePagePanel.cs
```

---

## Design Reference

설계 원본: `docs/ui-base-page-design.md`

> 설계 문서는 초기 설계 시점의 기록이며 구 클래스명(`UIPageCanvasBase` / `UIPagePanelBase`)을 사용한다.
> 최종 클래스명은 이 스킬 문서(SSOT)를 따른다.

---

## Related

- Parent: `../SKILL.md`
- Canvas System: `../10-base-system/11-ui-canvas-system/SKILL.md`
- UITweenSystem: `../22-ui-tween-system/00-overview/SKILL.md`
