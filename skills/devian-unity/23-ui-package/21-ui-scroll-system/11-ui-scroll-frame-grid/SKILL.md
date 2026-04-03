# 11-ui-scroll-frame-grid

Status: ACTIVE
AppliesTo: v3

---

## Overview

### Purpose

`UIScrollGridFrame`는 N열 grid section 구현체다.
`UIBaseFrame`를 상속하고 `IUIScrollSection`을 구현하며,
`UIScrollContainer`가 계산한 logical row enter/exit 요청에 따라 row 단위로 cell을 spawn/despawn 한다.

현재 구조에서 `UIScrollGridFrame`는 **row renderer**이지 **scroll virtualizer**가 아니다.

### Scope

**Includes:**
- `UIScrollGridFrame` — `IUIScrollSection` grid section 구현
- `UIScrollGridCell` — pooled cell component
- active row dictionary 기반 row bind/unbind/refresh

**Excludes:**
- `ScrollRect` 구독
- visible range 계산
- viewport/scroll position 계산
- section collection / overall layout

---

## SSOT

### Code Path

```
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Scroll/
├── UIScrollGridFrame.cs
└── UIScrollGridCell.cs
```

### Class Signatures

#### UIScrollGridFrame

```csharp
namespace Devian
{
    public class UIScrollGridFrame : UIBaseFrame, IUIScrollSection
    {
        [SerializeField] private UI_SCROLL_CELL_ID _cellPrefabId;
        [SerializeField] private int _columnCount = 4;
        [SerializeField] private int _minimumLineCount = 0;
        [SerializeField] private Vector2 _cellSize = new Vector2(200, 200);
        [SerializeField] private float _rowSpacing = 10f;

        public string CellPrefabName { get; set; }  // UI_SCROLL_CELL_ID.Value 기반
        public int ColumnCount { get; set; }
        public int MinimumLineCount { get; set; }
        public Vector2 CellSize { get; set; }
        public float RowSpacing { get; set; }  // Y축만. X축은 자동 계산.

        public int CellCount { get; }        // actual data count
        public int DataRowCount { get; }
        public int RowCount { get; }
        public int RenderCellCount { get; }

        public void SetCellCount(int count);
        public void SetMinimumLineCount(int lineCount);
        public bool HasDataAt(int cellIndex);
        public override float GetWidth();
        public override float GetHeight();

        public int GetLogicalRowCount();
        public float GetLogicalRowMainAxisSize(int localRowIndex);
        public float GetLogicalRowSpacing();
        public void ApplySectionLayout(in UIUIScrollSectionLayout layout);
        public void BindRow(in UIScrollRowLayout rowLayout);
        public void UnbindRow(int localRowIndex);
        public void RefreshRow(int localRowIndex);
        public void ClearSection();

        public Action<UIScrollGridCell, int> onBindCell;
        public Action<UIScrollGridCell> onUnbindCell;
    }
}
```

#### UIScrollGridCell

```csharp
namespace Devian
{
    [RequireComponent(typeof(RectTransform))]
    public class UIScrollGridCell : MonoBehaviour, IPoolable
    {
        public int CellIndex { get; }
        public RectTransform rectTransform { get; }

        public void Show(int cellIndex);
        public void Hide();

        public void OnPoolSpawned();
        public void OnPoolDespawned();
    }
}
```

---

## Runtime Model

### Size

- `GetWidth()` = parent RectTransform의 너비 (grid width는 항상 parent width와 동일)
- `GetHeight()` = `rowCount * cellSize.y + (rowCount - 1) * rowSpacing`
- X축 spacing = `(frameWidth - columnCount * cellSize.x) / max(1, columnCount - 1)` — 자동 계산, 셀을 균일 분배
- `CellCount` = actual data cell count
- `DataRowCount` = `CeilToInt(CellCount / (float)ColumnCount)` (`ColumnCount > 0`인 경우)
- `RowCount` = `max(MinimumLineCount, DataRowCount)` (`ColumnCount <= 0`이면 `0`)
- `RenderCellCount` = `RowCount * ColumnCount`
- `RectTransform.sizeDelta`는 init 및 count/size setter 변경 시 **height만** (`sizeDelta.y = GetHeight()`) 갱신된다. width는 건드리지 않는다
- init 완료 후 `CellCount`, `MinimumLineCount`, `ColumnCount`, `CellSize`, `Spacing`이 바뀌면 parent `UIScrollContainer.Rebuild()`를 자동 요청한다

### IUIScrollSection Model

- `GetLogicalRowCount()` = `RowCount`
- `GetLogicalRowMainAxisSize(localRowIndex)` = `_cellSize.y`
- `GetLogicalRowSpacing()` = `_rowSpacing`
- `ApplySectionLayout(...)`는 grid holder의 section 위치를 적용하고 active 상태를 유지한다
- `BindRow(...)`는 해당 row의 full-row cell들을 spawn/bind 한다
- `UnbindRow(localRowIndex)`는 해당 row의 cell들을 unbind/despawn 한다
- `RefreshRow(localRowIndex)`는 active row의 cell만 재바인딩한다
- `ClearSection()`는 active row 전체를 정리한다

### Row Ownership

visible row 판단은 하지 않는다.
container가 "이 row를 보여라/숨겨라/새로고침하라"를 결정하고, grid는 그 요청만 수행한다.

### Active State

- active row는 `Dictionary<int, List<UIScrollGridCell>> _activeRows`로 관리한다
- key는 `localRowIndex`
- value는 해당 row에 현재 spawn된 cell 목록이다

### BindRow

`BindRow(in UIScrollRowLayout rowLayout)` 동작:

1. `localRowIndex`가 `0 <= index < RowCount`인지 검사한다
2. `localRowIndex`에서 row 시작 cell index 계산
3. 항상 `ColumnCount`개 cell을 생성한다
4. `BundlePool.Spawn<UIScrollGridCell>(..., parent: frame.transform)`로 cell 생성
5. anchor/pivot/size 적용
6. `CalculateAutoXSpacing()`으로 X축 간격 자동 계산 후 `rowLayout.RowMainAxisPosition - section.SectionMainAxisPosition` 기반 frame local 위치 계산
7. `cell.Show(index)` 후 `onBindCell(cell, index)` 호출

주의:

- 마지막 row도 partial row가 아니라 full-row로 렌더된다
- `cellIndex >= CellCount`인 cell은 placeholder slot이다
- placeholder 판정 helper로 `HasDataAt(cellIndex)`를 제공한다

### UnbindRow

`UnbindRow(localRowIndex)` 동작:

1. active row lookup
2. 각 cell에 대해 `Hide()`
3. `onUnbindCell(cell)` 호출
4. `BundlePool.Despawn(cell)`
5. row entry 제거

### RefreshRow

- invalid row면 `UnbindRow(localRowIndex)` 후 return
- active row만 처리
- 각 cell에 대해 `Hide -> onUnbindCell -> Show -> onBindCell` 순으로 재바인딩

### Runtime Layout Changes

- `SetCellCount(...)`, `SetMinimumLineCount(...)`, `ColumnCount`, `CellSize`, `RowSpacing` 변경은 먼저 frame `RectTransform` sizeDelta의 **height만** (`sizeDelta.y = GetHeight()`) 동기화한다. width는 변경하지 않는다
- play mode에서 parent `UIScrollContainer`가 이미 초기화된 상태라면 `Rebuild()`를 자동 요청한다
- 그래서 init 이후 data count가 늘어나 row 수가 바뀌는 경우에도 scroll content size와 visible row 계산이 같이 갱신된다

### ClearSection

- 모든 active row의 cell을 unbind/despawn
- `_activeRows.Clear()`
- grid holder는 active 상태를 유지한다

---

## Virtualization Boundary

| 역할 | 담당 |
|------|------|
| `ScrollRect` 구독 | `UIScrollContainer` |
| visible logical row 계산 | `UIScrollContainer` |
| row bind/unbind 요청 | `UIScrollContainer` |
| row 내부 cell spawn/despawn | `UIScrollGridFrame` |
| cell 데이터 bind/unbind | `UIScrollGridFrame` + `onBindCell` / `onUnbindCell` |
| placeholder 판단 | consumer (`HasDataAt(cellIndex)` 또는 `cellIndex >= CellCount`) |

---

## Dependencies

| Dependency | Location |
|------------|----------|
| `UIBaseFrame` | `framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Base/UIBaseFrame.cs` |
| `IUIScrollSection` | `framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Scroll/IUIScrollSection.cs` |
| `UIScrollRowLayout` | `framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Scroll/UIScrollRowLayout.cs` |
| `UIUIScrollSectionLayout` | `framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Scroll/UIScrollSectionLayout.cs` |
| `BundlePool` | `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/CommonPackage/Runtime/Unity/Pool/Factory/BundlePool.cs` |
| `IPoolable` | `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/CommonPackage/Runtime/Unity/Pool/IPoolable.cs` |

---

## Reference

- Parent: `skills/devian-unity/23-ui-package/SKILL.md`
- Scroll container: `skills/devian-unity/23-ui-package/21-ui-scroll-system/10-ui-scroll-container/SKILL.md`
- Frame base: `skills/devian-unity/23-ui-package/10-base-system/11-ui-canvas-system/SKILL.md`
