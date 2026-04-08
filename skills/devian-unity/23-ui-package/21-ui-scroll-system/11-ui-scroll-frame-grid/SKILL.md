# 11-ui-scroll-frame-grid

Status: ACTIVE
AppliesTo: v3

---

## Overview

`UIScrollGridFrame`는 `UIScrollContainer`가 계산한 logical row enter/exit 요청만 받아
row 내부 cell을 spawn/show/hide/despawn 하는 grid section renderer다.
virtualization owner는 아니다.

현재 구현 원칙:

- `UIScrollGridFrame`는 layout, pool, lifecycle bridge만 담당한다.
- 실제 데이터 bind/reset은 `UIScrollGridCell.onShow(int cellIndex)` / `onHide()`가 담당한다.
- scroll 쪽은 별도 init semantics를 만들지 않고 `UIBaseFrame._Init()` / `_InitComplete()`를 재사용한다.

---

## SSOT

### Code Path

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Scroll/
├── UIScrollGridFrame.cs
└── UIScrollGridCell.cs
```

### 3-Path Mirror

| 경로 | 역할 |
|------|------|
| `framework-cs/upm/com.devian.foundation/Samples~/UIPackage/` | UPM 정본 |
| `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/UIPackage/` | Packages mirror |
| `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/{version}/UIPackage/` | Assets/Samples 구현 |

---

## Public Model

### UIScrollGridCell

```csharp
namespace Devian
{
    [RequireComponent(typeof(RectTransform))]
    public class UIScrollGridCell : UIBaseFrame, IPoolable
    {
        public int CellIndex { get; }

        public void Show(int cellIndex);
        public void Hide();

        public void OnPoolSpawned();
        public void OnPoolDespawned();

        protected virtual void onInit();
        protected virtual void onInitComplete();
        protected virtual void onShow(int cellIndex);
        protected virtual void onHide();
        protected virtual void onPoolSpawned();
        protected virtual void onPoolDespawned();
    }
}
```

의미:

- `onInit()` / `onInitComplete()` = semantic init
- `onShow(int cellIndex)` = 현재 index 기준 full bind
- `onHide()` = reset / unbind
- `CellIndex`는 현재 bind index다

### UIScrollGridFrame

```csharp
namespace Devian
{
    public class UIScrollGridFrame : UIBaseFrame, IUIScrollSection
    {
        public string CellPrefabName { get; set; }
        public int ColumnCount { get; set; }
        public int MinimumLineCount { get; set; }
        public Vector2 CellSize { get; set; }
        public float RowSpacing { get; set; }
        public RectOffset Padding { get; set; }

        public int CellCount { get; }
        public int DataRowCount { get; }
        public int RowCount { get; }
        public int RenderCellCount { get; }

        public void SetCellCount(int count);
        public void SetMinimumLineCount(int lineCount);
        public bool HasDataAt(int cellIndex);

        public UIScrollGridCell Spawn(...);
        public T Spawn<T>(...) where T : UIScrollGridCell;
        public void Despawn<T>(T cell) where T : UIScrollGridCell;

        protected virtual UIScrollGridCell createCell(Transform parent);
    }
}
```

### Typed Alias

```csharp
namespace Devian
{
    public abstract class UIScrollGridFrame<TCell> : UIScrollGridFrame
        where TCell : UIScrollGridCell
    {
    }
}
```

`UIScrollGridFrame<TCell>`는 타입 의미만 주는 thin wrapper다.
bind hook을 추가하지 않는다.

---

## Init Semantics

`UIScrollGridFrame`와 `UIScrollGridCell` 모두 별도 scroll init 시스템을 만들지 않는다.
기존 `UIBaseFrame._Init()` / `_InitComplete()`가 semantic boundary다.

해석 규칙:

- owner tree가 이미 initialized 상태면 cell spawn 직후 `cell._Init(canvas)`가 즉시 수행될 수 있다
- owner frame이 이미 init complete 상태면 같은 경로에서 `cell._InitComplete()`도 즉시 수행될 수 있다
- frame이 아직 init complete 전이면 cell은 `onInit()`까지만 먼저 수행되고, frame `onInitComplete()` 시점에 active cell들이 `_InitComplete()` 된다

실무 규칙:

- child UI 구조 생성 / ref wiring은 `cell.onInit()`에 둔다
- `cell.onInit()` 안에서 새 `UIComponentBase` / `UIBaseFrame` subtree를 만들었다면 `InitDynamicSubtree(root)`를 호출해 기존 init 체계에 편입한다
- 현재 데이터 bind는 `cell.onShow(int cellIndex)`에 둔다
- 이전 bind 정리는 `cell.onHide()`에 둔다

---

## Runtime Flow

### Row Bind

internal `IUIScrollSection.BindRow(...)` 흐름:

1. row index 유효성 검사
2. 이미 active면 기존 row unbind
3. row마다 `ColumnCount`개 cell 생성
4. `createCell(transform)` 호출 — cell은 frame의 child로 생성된다
5. spawn 직후 `ensureCellInitialized()`로 `cell._Init(canvas)` / 필요 시 `cell._InitComplete()` 연결
6. anchor/pivot/size 적용
7. position은 frame 로컬좌표로 계산: `localMainPos = mainPos - sectionMainAxisPosition`
8. `cell.Show(cellIndex)` 호출

### Row Unbind

internal `IUIScrollSection.UnbindRow(...)` 흐름:

1. active row lookup
2. 각 cell에 대해 `Hide()` 후 `BundlePool.Despawn(cell)`

### Row Refresh

active row만 처리한다.

- `cell.Hide()`
- `cell.Show(cellIndex)`

즉 refresh에서도 `onHide()`가 reset 경계다.

### Section Clear

- 모든 active row cell을 hide/despawn
- `_activeRows.Clear()`
- holder는 active 상태를 유지한다

---

## Layout Rules

- `GetWidth()`는 `UIBaseFrame` base 구현 사용
- `GetHeight()` =
  - `padding.top + rowCount * cellSize.y + (rowCount - 1) * rowSpacing + padding.bottom`
- X축 spacing은 frame width 기준 자동 계산:
  - `(frameWidth - padding.left - padding.right - columnCount * cellSize.x) / max(1, columnCount - 1)`
- `left/right`를 제외한 내부 영역에서 cell 간격을 균등 분배한다
- row 0은 `padding.top`, 마지막 row는 `padding.bottom`을 흡수한다
- 마지막 row도 partial row가 아니라 full row renderer다
- 데이터가 없는 slot은 placeholder로 남고, consumer는 `HasDataAt(cellIndex)`로 판정한다

---

## Pool Rules

- `Spawn()`은 `_cellPrefabId` prefab의 실제 pool type을 해석한 뒤 그 concrete type으로 spawn한다
- prefab 대표 `IPoolable` component는 반드시 `UIScrollGridCell` 하위 타입이어야 한다
- `Spawn<T>()`는 typed facade다. 실제 생성은 항상 prefab concrete type 기준으로 수행된다
- `OnPoolSpawned()`는 `CellIndex = -1` 후 `_HandlePoolSpawned()` bridge를 호출한다
- `OnPoolDespawned()`는 `CellIndex = -1` 후 `_HandlePoolDespawned()` bridge를 호출한다
- pooled cell은 despawn 시 `UIBaseFrame._ResetForPool()`로 init state가 reset된다

---

## Recommended Usage

### Frame

frame subclass는 보통 layout과 count만 설정한다.

```csharp
public sealed class HeroEquipGridFrame : UIScrollGridFrame<HeroEquipGridCell>
{
    protected override void onInit()
    {
        SetCellCount(InventoryManager.Instance.EquippedItems.Count);
    }
}
```

### Cell

실제 데이터 bind는 cell이 `cellIndex`로 직접 조회한다.

```csharp
public sealed class HeroEquipGridCell : UIScrollGridCell
{
    protected override void onInit()
    {
        // child UI 생성 / ref wiring
    }

    protected override void onShow(int cellIndex)
    {
        var items = InventoryManager.Instance.EquippedItems;
        if (cellIndex < 0 || cellIndex >= items.Count)
            return;

        var item = items[cellIndex];
        // bind
    }

    protected override void onHide()
    {
        // reset
    }
}
```

즉:

- frame은 데이터 전달하지 않는다
- cell이 `cellIndex`로 직접 조회한다
- `onShow(int cellIndex)`만으로 bind를 해결한다

---

## Reference

- Parent: `skills/devian-unity/23-ui-package/SKILL.md`
- Scroll container: `skills/devian-unity/23-ui-package/21-ui-scroll-system/10-ui-scroll-container/SKILL.md`
- Frame lifecycle base: `skills/devian-unity/23-ui-package/10-base-system/11-ui-canvas-system/SKILL.md`
