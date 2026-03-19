# 23-ui-frame-grid

Status: ACTIVE
AppliesTo: v3

---

## Overview

### Purpose

`UIGridFrame`는 N열 grid section 구현체다.
`UIBaseFrame`를 상속하고 `IUIScrollSection`을 구현하며,
`UIScrollContainer`가 계산한 logical row enter/exit 요청에 따라 row 단위로 cell을 spawn/despawn 한다.

현재 구조에서 `UIGridFrame`는 **row renderer**이지 **scroll virtualizer**가 아니다.

### Scope

**Includes:**
- `UIGridFrame` — `IUIScrollSection` grid section 구현
- `UIGridCell` — pooled cell component
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
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Container/
├── UIGridFrame.cs
└── UIGridCell.cs
```

### Class Signatures

#### UIGridFrame

```csharp
namespace Devian
{
    public class UIGridFrame : UIBaseFrame, IUIScrollSection
    {
        [SerializeField] private string _cellPrefabName;
        [SerializeField] private int _columnCount = 4;
        [SerializeField] private Vector2 _cellSize = new Vector2(200, 200);
        [SerializeField] private Vector2 _spacing = new Vector2(10, 10);

        public string CellPrefabName { get; set; }
        public int ColumnCount { get; set; }
        public Vector2 CellSize { get; set; }
        public Vector2 Spacing { get; set; }

        public int CellCount { get; }
        public int RowCount { get; }

        public void SetCellCount(int count);
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

        public Action<UIGridCell, int> onBindCell;
        public Action<UIGridCell> onUnbindCell;
    }
}
```

#### UIGridCell

```csharp
namespace Devian
{
    [RequireComponent(typeof(RectTransform))]
    public class UIGridCell : MonoBehaviour, IPoolable
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

- `GetWidth()` = `columnCount * cellSize.x + (columnCount - 1) * spacing.x`
- `GetHeight()` = `rowCount * cellSize.y + (rowCount - 1) * spacing.y`
- `RowCount` = `CeilToInt(CellCount / (float)ColumnCount)`

### IUIScrollSection Model

- `GetLogicalRowCount()` = `RowCount`
- `GetLogicalRowMainAxisSize(localRowIndex)` = `_cellSize.y`
- `GetLogicalRowSpacing()` = `_spacing.y`
- `ApplySectionLayout(...)`는 초기 숨김 상태를 적용한다
- `BindRow(...)`는 해당 row의 cell들을 spawn/bind 한다
- `UnbindRow(localRowIndex)`는 해당 row의 cell들을 unbind/despawn 한다
- `RefreshRow(localRowIndex)`는 active row의 cell만 재바인딩한다
- `ClearSection()`는 active row 전체를 정리한다

### Row Ownership

visible row 판단은 하지 않는다.
container가 "이 row를 보여라/숨겨라/새로고침하라"를 결정하고, grid는 그 요청만 수행한다.

### Active State

- active row는 `Dictionary<int, List<UIGridCell>> _activeRows`로 관리한다
- key는 `localRowIndex`
- value는 해당 row에 현재 spawn된 cell 목록이다

### BindRow

`BindRow(in UIScrollRowLayout rowLayout)` 동작:

1. `localRowIndex`에서 row 시작 cell index 계산
2. row에 필요한 cell 수 계산
3. `BundlePool.Spawn<UIGridCell>(..., parent: rowLayout.Content)`로 cell 생성
4. anchor/pivot/size 적용
5. `rowLayout.Direction`과 `rowLayout.RowMainAxisPosition` 기준으로 위치 계산
6. `cell.Show(index)` 후 `onBindCell(cell, index)` 호출

### UnbindRow

`UnbindRow(localRowIndex)` 동작:

1. active row lookup
2. 각 cell에 대해 `Hide()`
3. `onUnbindCell(cell)` 호출
4. `BundlePool.Despawn(cell)`
5. row entry 제거

### RefreshRow

- active row만 처리
- 각 cell에 대해 `Hide -> onUnbindCell -> Show -> onBindCell` 순으로 재바인딩

### ClearSection

- 모든 active row의 cell을 unbind/despawn
- `_activeRows.Clear()`
- config holder인 grid `gameObject`를 다시 활성화한다

---

## Virtualization Boundary

| 역할 | 담당 |
|------|------|
| `ScrollRect` 구독 | `UIScrollContainer` |
| visible logical row 계산 | `UIScrollContainer` |
| row bind/unbind 요청 | `UIScrollContainer` |
| row 내부 cell spawn/despawn | `UIGridFrame` |
| cell 데이터 bind/unbind | `UIGridFrame` + `onBindCell` / `onUnbindCell` |

---

## Dependencies

| Dependency | Location |
|------------|----------|
| `UIBaseFrame` | `framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Container/UIBaseFrame.cs` |
| `IUIScrollSection` | `framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Container/IUIScrollSection.cs` |
| `UIScrollRowLayout` | `framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Container/UIScrollRowLayout.cs` |
| `UIUIScrollSectionLayout` | `framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Container/UIScrollSectionLayout.cs` |
| `BundlePool` | `framework-cs/upm/com.devian.foundation/Samples~/CommonPackage/Runtime/Unity/Pool/Factory/BundlePool.cs` |
| `IPoolable` | `framework-cs/upm/com.devian.foundation/Samples~/CommonPackage/Runtime/Unity/Pool/IPoolable.cs` |

---

## Reference

- Parent: `skills/devian-unity/23-ui-package/SKILL.md`
- Scroll container: `skills/devian-unity/23-ui-package/22-ui-container-scroll/SKILL.md`
- Frame base: `skills/devian-unity/23-ui-package/11-ui-canvas-system/SKILL.md`
