# 14-page-system — Canvas & Panel

Status: ACTIVE
AppliesTo: v11

---

## Overview

### Purpose

Page 전환 전용 `UIBasePageCanvas` / `UIBasePagePanel` 계약을 정의한다.
`UIBaseCanvas` / `UIBasePanel`을 상속하며, page host/coordinator 책임은 canvas가 가진다.

### Terms

| Term | Definition |
|------|------------|
| **UIBasePageCanvas** | 비제네릭 page canvas base. page 수집, current page 추적, ShowPage, transition orchestration을 담당한다 |
| **UIBasePageCanvas\<TCanvas\>** | singleton bridge. `static Instance`를 제공한다 |
| **UIBasePagePanel** | page identity, enter/exit transition, page state, page hook을 담당하는 비제네릭 page panel base |
| **UIBasePagePanel\<TCanvas\>** | 강타입 owner page canvas 참조를 제공하는 typed panel layer |
| **UIPageState** | `Hidden`, `Entering`, `Visible`, `Exiting` |
| **UIPageTransitionDirection** | `None`, `Left`, `Right` |

---

## Policy

### Page Navigation Policy

| Rule | Description |
|------|-------------|
| **MUST** | 안정 상태에서 current page는 정확히 0개 또는 1개다 |
| **MUST** | transition 중에는 outgoing/incoming 2개가 잠시 동시 active일 수 있다 |
| **MUST** | 외부 navigation은 반드시 `UIBasePageCanvas.ShowPage()`를 통해서만 수행한다 |
| **MUST** | `pageIndex`는 serialized inspector 값이다. runtime 계산값이 아니다 |
| **MUST** | 중복 `pageIndex`는 invalid다 |
| **MUST NOT** | 외부 코드가 `page.Show()` / `page.Hide()`를 page navigation 목적으로 직접 호출 |
| **MUST NOT** | 외부 코드가 page `_Enter` / `_Exit` 내부 API를 직접 호출 |

### Transition Policy

| Rule | Description |
|------|-------------|
| **MUST** | transition 재생은 same-GO `UITransitionPlayer` 하나로 처리한다 |
| **MUST** | `animated == false`이면 transition 없이 즉시 완료한다 |
| **MUST** | 해당 방향 transition ID가 null/invalid면 즉시 완료 fallback 처리한다 |

### Concurrent Request Policy

| Rule | Description |
|------|-------------|
| **MUST** | `latest wins` — transition 중 새 요청 시 현재 transition cancel → 정규화 → 새 transition 시작 |
| **MUST NOT** | transition 중 요청 무시 또는 queue 순차 처리 |

### Lifecycle Policy

| Rule | Description |
|------|-------------|
| **MUST** | `UIBasePageCanvas`는 `onInitComplete()`에서 page 수집·정규화를 수행한다. subclass override 시 `base.onInitComplete()`를 호출해야 한다 |
| **MUST** | `UIBasePageCanvas`는 `onPoolSpawned()` / `onPoolDespawned()`를 sealed override하여 page state를 복구/리셋한다 |
| **MUST** | subclass hook은 `onPageCanvasInitComplete()` / `onPageCanvasPoolSpawned()` / `onPageCanvasPoolDespawned()`를 사용한다 |
| **MUST** | `UIBasePagePanel`은 `onInitFromCanvas()`를 sealed override하여 typed owner bridge를 수행한다 |

---

## SSOT

### Implementation Location (3-path mirror)

| 경로 | 역할 |
|------|------|
| `framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Page/` | UPM (정본) |
| `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/UIPackage/Runtime/Page/` | Packages (sync) |
| `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/{version}/UIPackage/Runtime/Page/` | Assets/Samples (import) |

### Canonical Files

```text
framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/UIPackage/Runtime/Page/
├── UIBasePageCanvas.cs
└── UIBasePagePanel.cs
```

### File List

| File | Purpose |
|------|---------|
| `UIBasePageCanvas.cs` | `UIBasePageCanvas` + `UIBasePageCanvas<TCanvas>` |
| `UIBasePagePanel.cs` | `UIBasePagePanel` + `UIBasePagePanel<TCanvas>` + `UIPageState` + `UIPageTransitionDirection` |

---

## Public API

### UIBasePageCanvas

```csharp
namespace Devian
{
    public abstract class UIBasePageCanvas : UIBaseCanvas
    {
        public UIBasePagePanel currentPage { get; }
        public bool isTransitioning { get; }

        public void ShowPage(int pageIndex, bool animated = true);
        public void ShowPage(UIBasePagePanel page, bool animated = true);
        public bool TryGetPage(int pageIndex, out UIBasePagePanel page);

        protected virtual void onPageCanvasInitComplete();
        protected virtual void onPageCanvasPoolSpawned();
        protected virtual void onPageCanvasPoolDespawned();
    }
}
```

### UIBasePageCanvas\<TCanvas\>

```csharp
namespace Devian
{
    public abstract class UIBasePageCanvas<TCanvas> : UIBasePageCanvas
        where TCanvas : UIBasePageCanvas
    {
        public static TCanvas Instance { get; }
    }
}
```

`UIBaseCanvas<TCanvas>`를 함께 상속할 수 없으므로 singleton bridge를 자체 구현한다.

### UIBasePagePanel

```csharp
namespace Devian
{
    [RequireComponent(typeof(UITransitionPlayer))]
    public abstract class UIBasePagePanel : UIBasePanel
    {
        public int pageIndex { get; }
        public bool isCurrentPage { get; }
        public UIPageState pageState { get; }
        public UIBasePageCanvas ownerCanvas { get; }

        protected virtual void onInit(UIBasePageCanvas canvas);
        protected virtual void onPageWillEnter(UIPageTransitionDirection direction, UIBasePagePanel fromPage);
        protected virtual void onPageEntered(UIPageTransitionDirection direction, UIBasePagePanel fromPage);
        protected virtual void onPageWillExit(UIPageTransitionDirection direction, UIBasePagePanel toPage);
        protected virtual void onPageExited(UIPageTransitionDirection direction, UIBasePagePanel toPage);
    }
}
```

### UIBasePagePanel\<TCanvas\>

```csharp
namespace Devian
{
    public abstract class UIBasePagePanel<TCanvas> : UIBasePagePanel
        where TCanvas : UIBasePageCanvas
    {
        protected new TCanvas ownerCanvas { get; }

        protected sealed override void onInit(UIBasePageCanvas canvas);
        protected virtual void onInit(TCanvas canvas);
    }
}
```

---

## Navigation Sequence

### Transition Rules

| Current | Target | Result |
|---------|--------|--------|
| 없음 | 새 page | 즉시 표시, transition 없음 |
| 동일 page | 동일 page | no-op |
| 작은 index | 큰 index | current는 Left로 exit, target은 Left에서 enter |
| 큰 index | 작은 index | current는 Right로 exit, target은 Right에서 enter |

### Transition Preset Mapping

| 동작 | 방향 | 사용할 preset |
|------|------|---------------|
| enter | Left | `_enterFromLeftTransitionId` |
| enter | Right | `_enterFromRightTransitionId` |
| exit | Left | `_exitToLeftTransitionId` |
| exit | Right | `_exitToRightTransitionId` |

### ShowPage Sequence

```text
ShowPage(target, animated)
├── target == null → warn, return
├── !IsOwnedPage(target) → warn, return
├── isTransitioning → NormalizeToCurrentPage()
├── currentPage == target → no-op
├── currentPage == null → NormalizeToPage(target), return
└── StartTransition(target, animated)
    ├── direction = current.pageIndex < target.pageIndex ? Left : Right
    ├── _navigationVersion++
    ├── currentPage = target (즉시 교체)
    ├── 나머지 page → _NormalizeHidden()
    ├── target._Enter(direction, animated, fromPage, CompleteStep)
    └── fromPage._Exit(direction, animated, target, CompleteStep)
        └── 양쪽 완료 시 NormalizeToPage(target)
```

---

## Pool Contract

- `UIBasePageCanvas`는 `onInitComplete` / `onPoolSpawned` / `onPoolDespawned`를 sealed override
- pool despawn 시: 모든 page transition cancel, `_currentPage = null`, `isTransitioning = false`, page 리스트 clear
- pool spawn 시: page 재수집, 초기 상태 정규화 (active page 중 가장 작은 index를 current로)
- `UIBasePagePanel`은 별도 pool reset 불필요 — canvas가 page state를 정규화함

---

## DoD

- [ ] `UIBasePageCanvas`가 `UIBaseCanvas`를 상속하고 page host 로직을 포함한다
- [ ] `UIBasePagePanel`이 `UIBasePanel`을 상속하고 page identity/transition을 포함한다
- [ ] transition은 same-GO `UITransitionPlayer` 기반이다
- [ ] 동시 요청은 `latest wins` 정책이다
- [ ] 초기 상태 정규화가 중복 active page를 정리한다
- [ ] `onInitComplete()` override 시 `base.onInitComplete()` 호출 규약이 유지된다
- [ ] 컴파일 오류 0개

---

## Reference

- Overview: `../00-overview/SKILL.md`
- Base Canvas System: `../../10-base-system/11-ui-canvas-system/SKILL.md`
- UITransitionPlayer: `../../22-ui-tween-system/14-ui-transition-player/SKILL.md`
- Parent Policy: `../../01-policy/SKILL.md`
