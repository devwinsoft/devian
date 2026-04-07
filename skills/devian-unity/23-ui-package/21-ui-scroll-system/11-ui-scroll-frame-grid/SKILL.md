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
        [SerializeField] private RectOffset _padding = new RectOffset();

        public string CellPrefabName { get; set; }  // UI_SCROLL_CELL_ID.Value 기반
        public int ColumnCount { get; set; }
        public int MinimumLineCount { get; set; }
        public Vector2 CellSize { get; set; }
        public float RowSpacing { get; set; }  // Y축만. X축은 자동 계산.
        public RectOffset Padding { get; set; }  // 그리드 내부 여백 (top/bottom/left/right)

        public int CellCount { get; }        // actual data count
        public int DataRowCount { get; }
        public int RowCount { get; }
        public int RenderCellCount { get; }

        public void SetCellCount(int count);
        public void SetMinimumLineCount(int lineCount);
        public bool HasDataAt(int cellIndex);
        public UIScrollGridCell Spawn(
            Transform parent = null,
            Vector3 position = default,
            Quaternion rotation = default,
            PoolOptions options = default);
        public T Spawn<T>(
            Transform parent = null,
            Vector3 position = default,
            Quaternion rotation = default,
            PoolOptions options = default)
            where T : UIScrollGridCell;
        public void Despawn<T>(T cell)
            where T : UIScrollGridCell;
        // GetWidth(): base 구현 사용 (rectTransform.rect.width). override 하지 않는다.
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
        protected virtual void onPoolSpawned();
        protected virtual void onPoolDespawned();
    }
}
```

---

## Runtime Model

### Size

- `GetWidth()` = 자신의 RectTransform 너비 (base 구현 사용, override 하지 않음)
- `GetHeight()` = `padding.top + rowCount * cellSize.y + (rowCount - 1) * rowSpacing + padding.bottom`
- X축 spacing = `(frameWidth - padding.left - padding.right - columnCount * cellSize.x) / max(1, columnCount - 1)` — 자동 계산, 셀을 균일 분배
- `CellCount` = actual data cell count
- `DataRowCount` = `CeilToInt(CellCount / (float)ColumnCount)` (`ColumnCount > 0`인 경우)
- `RowCount` = `max(MinimumLineCount, DataRowCount)` (`ColumnCount <= 0`이면 `0`)
- `RenderCellCount` = `RowCount * ColumnCount`
- `RectTransform.sizeDelta`는 init 및 count/size setter 변경 시 **height만** (`sizeDelta.y = GetHeight()`) 갱신된다. width는 건드리지 않는다
- init 완료 후 `CellCount`, `MinimumLineCount`, `ColumnCount`, `CellSize`, `Spacing`이 바뀌면 parent `UIScrollContainer.Rebuild()`를 자동 요청한다
- `Spawn()`은 `_cellPrefabId.Value`의 prefab 실제 pool type을 해석한 뒤 그 concrete type으로 pooled cell을 생성한다. prefab 대표 `IPoolable` component는 반드시 `UIScrollGridCell` 하위 타입이어야 한다
- `Spawn<T>() where T : UIScrollGridCell`는 typed facade다. 내부 생성은 항상 prefab 실제 pool type 기준으로 수행되고, 반환 타입만 `T`로 검증한다
- `Despawn<T>(T cell)`는 typed facade이며, cell이 바인딩 상태면 먼저 `Hide()`와 `onUnbindCell`을 실행한 뒤 `BundlePool.Despawn(cell)`에 위임한다
- grid cell reset 경계는 `onHide()`다. `onShow()`는 현재 데이터에 대한 full bind만 수행한다

### IUIScrollSection Model

- `GetLogicalRowCount()` = `RowCount`
- `GetLogicalRowMainAxisSize(localRowIndex)`:
  - row 0: `_padding.top + _cellSize.y`
  - 마지막 row: `_cellSize.y + _padding.bottom`
  - row가 1개뿐: `_padding.top + _cellSize.y + _padding.bottom`
  - 그 외: `_cellSize.y`
  - padding을 첫/끝 row 크기에 흡수하여 IUIScrollSection 인터페이스 변경 없이 Container에 전달한다
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
4. `Spawn(parent: rowLayout.Content)`로 cell 생성한다. 이때 `_cellPrefabId` prefab의 실제 pool type을 사용하므로 `UIScrollGridCell` subclass prefab도 그대로 지원된다
5. anchor/pivot/size 적용
6. `CalculateAutoXSpacing()`으로 X축 간격 자동 계산
7. cell X 위치: `_padding.left + c * (cellSize.x + xSpacing)`
8. cell main axis 위치: row 0이면 `rowLayout.RowMainAxisPosition + _padding.top`, 그 외 `rowLayout.RowMainAxisPosition`
9. `cell.Show(index)` 후 `onBindCell(cell, index)` 호출

주의:

- 마지막 row도 partial row가 아니라 full-row로 렌더된다
- `cellIndex >= CellCount`인 cell은 placeholder slot이다
- placeholder 판정 helper로 `HasDataAt(cellIndex)`를 제공한다

### UnbindRow

`UnbindRow(localRowIndex)` 동작:

1. active row lookup
2. 각 cell에 대해 `Despawn(cell)`
3. `Despawn(cell)` 내부에서 `Hide() -> onUnbindCell -> BundlePool.Despawn()` 순서를 보장한다
5. row entry 제거

### RefreshRow

- invalid row면 `UnbindRow(localRowIndex)` 후 return
- active row만 처리
- 각 cell에 대해 `Hide -> onUnbindCell -> Show -> onBindCell` 순으로 재바인딩
- 이때 `Hide()`가 reset 경계이며, 다음 `Show()`는 이전 상태를 재사용하지 않고 full bind를 수행해야 한다

### Runtime Layout Changes

- `SetCellCount(...)`, `SetMinimumLineCount(...)`, `ColumnCount`, `CellSize`, `RowSpacing` 변경은 먼저 frame `RectTransform` sizeDelta의 **height만** (`sizeDelta.y = GetHeight()`) 동기화한다. width는 변경하지 않는다
- play mode에서 parent `UIScrollContainer`가 이미 초기화된 상태라면 `Rebuild()`를 자동 요청한다
- 그래서 init 이후 data count가 늘어나 row 수가 바뀌는 경우에도 scroll content size와 visible row 계산이 같이 갱신된다

### ClearSection

- 모든 active row의 cell을 unbind/despawn
- `_activeRows.Clear()`
- grid holder는 active 상태를 유지한다

### Cell Pool Lifecycle

- `UIScrollGridCell`은 public `OnPoolSpawned()` / `OnPoolDespawned()` bridge를 base에서 고정한다
- base bridge는 공통 reset(`CellIndex = -1`) 후 protected `onPoolSpawned()` / `onPoolDespawned()`를 호출한다
- cell 내용/UI reset은 `onHide()`가 담당한다. pool callback은 pooled lifecycle 보조 훅이지 bind reset 정본이 아니다
- 파생 cell은 public pool callback을 다시 구현하지 않고 protected hook만 override한다
- 따라서 `Spawn()` 또는 `Spawn<T>() where T : UIScrollGridCell`로 생성하면 base reset과 subclass hook이 모두 적용된다

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
