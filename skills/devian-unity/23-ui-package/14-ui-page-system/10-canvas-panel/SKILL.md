# 14-page-system — Canvas & Panel

Status: ACTIVE
AppliesTo: v11

---

## Overview

Page 시스템은 `UIBasePageCanvas`가 host 역할을 하고,
page는 아래 4개 base로 분리된다.

- `UIBasePagePanel : UIBasePanel`
- `UIBasePageMain : UIBasePagePanel`
- `UIBasePageSub : UIBasePagePanel`

`UIBasePageCanvas.ShowPage()`는 main만 전환한다.
sub는 각자 `Show()/Hide()`를 호출하지만 실제 owner 검증과 current 상태 관리는 canvas가 맡는다.

---

## Terms

| Term | Definition |
|------|------------|
| **UIBasePageCanvas** | `currentMain/currentSub`를 관리하는 page host |
| **UIBasePageMain** | 좌우 transition을 갖는 navigation 대상 main page |
| **UIBasePageSub** | 특정 main에 종속된 sub page |
| **UIBasePagePanel** | main/sub 공통 `pageIndex` + `ownerCanvas` bridge |
| **UIPageState** | `Hidden`, `Entering`, `Visible`, `Exiting` |
| **UIPageTransitionDirection** | `None`, `Left`, `Right` |

---

## Policy

### Main

- `ShowPage()`는 `UIBasePageMain`만 대상으로 동작한다.
- `currentMain`은 안정 상태에서 최대 1개다.
- `pageIndex`가 작은 main이 left, 큰 main이 right다.
- left 이동 규칙: enter from right, exit to left
- right 이동 규칙: enter from left, exit to right

### Sub

- `UIBasePageSub.pageIndex`는 자신이 종속되는 main의 index다.
- `sub.Show()`는 현재 main과 같은 `pageIndex`일 때만 유효하다.
- sub show 시 current main은 즉시 숨겨진다. 애니메이션 없음.
- sub hide 완료 후 current main은 즉시 복구된다. 애니메이션 없음.
- 동시에 visible한 sub는 최대 1개다.

### Main Switch

- main 전환 전 current sub는 즉시 닫힌다.
- sub 때문에 숨겨져 있던 old main은 전환 직전에 즉시 복구되어 exit transition에 참여한다.

### Lifecycle

- `UIBasePageCanvas`는 `onInitComplete()`에서 page 수집과 초기 정규화를 수행한다.
- subclass가 `onInitComplete()`를 override하면 `base.onInitComplete()`를 호출해야 한다.
- `initialPageIndex`는 main page 기준으로만 해석한다.

---

## Public API

### UIBasePageCanvas

```csharp
public abstract class UIBasePageCanvas : UIBaseCanvas
{
    public int initialPageIndex { get; }
    public UIBasePageMain currentMain { get; }
    public UIBasePageSub currentSub { get; }
    public bool isTransitioningMain { get; }

    public void ShowPage(int pageIndex, bool animated = true);
    public void ShowPage(UIBasePageMain page, bool animated = true);
    public bool TryGetMainPage(int pageIndex, out UIBasePageMain page);
}
```

### UIBasePagePanel

```csharp
public abstract class UIBasePagePanel : UIBasePanel
{
    public int pageIndex { get; }
    public UIBasePageCanvas ownerCanvas { get; }
}
```

### UIBasePageMain

```csharp
[RequireComponent(typeof(UITransitionPlayer))]
public abstract class UIBasePageMain : UIBasePagePanel
{
    public bool isCurrentMain { get; }
    public UIPageState pageState { get; }
}
```

### UIBasePageSub

```csharp
[RequireComponent(typeof(UITransitionPlayer))]
public abstract class UIBasePageSub : UIBasePagePanel
{
    public bool isCurrentSub { get; }

    public new void Show();
    public new void Hide();
}
```

---

## Sequence

### Main Show

```text
ShowPage(targetMain)
├── currentSub 즉시 close
├── old main 복구 (sub가 열려 있었던 경우)
├── currentMain == null → target 즉시 표시
└── old/new main directional transition
```

### Sub Show

```text
sub.Show()
├── ownerCanvas/currentMain 검사
├── pageIndex 일치 검사
├── 기존 currentSub 즉시 close
├── currentMain 즉시 hide
└── sub show transition
```

---

## Validation

- main `pageIndex` 중복 금지
- sub `pageIndex`는 대응 main이 반드시 존재해야 한다
- main/sub는 same-GO `UITransitionPlayer`가 있어야 한다
- `initialPageIndex >= 0`이면 대응 main이 존재해야 한다

---

## File Map

```text
Assets/Samples/Devian Foundation/0.1.0/UIPackage/Runtime/Page/
├── UIBasePageCanvas.cs
├── UIBasePagePanel.cs
├── UIBasePageMain.cs
└── UIBasePageSub.cs
```

---

## DoD

- [ ] `ShowPage()`가 main만 전환한다
- [ ] sub show/hide가 main hide/restore 규칙을 만족한다
- [ ] main switch 시 old sub가 즉시 닫힌다
- [ ] compile error가 0개다

---

## Reference

- Overview: `../00-overview/SKILL.md`
- Design Note: `/Users/maoshy/Documents/Projects/devian/docs/ui-base-page-design.md`
- Base Canvas System: `../../10-base-system/11-ui-canvas-system/SKILL.md`
