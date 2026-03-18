# 35-ui-plugin-scroll-system

Status: ACTIVE
AppliesTo: v1

---

## Overview

### Purpose

세로/가로 스크롤 혼합 레이아웃 시스템.
Container가 Content 하위의 Section(Grid / Frame)을 자동 수집하여 순서대로 배치하며,
Grid의 셀만 `BundlePool`로 재활용(virtualization)한다.
Frame은 Pool 대상이 아닌 고정 요소로, 뷰포트 밖이면 `SetActive(false)` 처리한다.

### Scope

**Includes:**
- UIComponentScrollContainer — MonoBehaviour, ScrollRect 소유, 가상화 엔진
- UIPlugInScrollGrid — N열 그리드 섹션 (MonoBehaviour, Content 자식, config holder)
- UIPlugInScrollFrame — 고정 섹션 (MonoBehaviour, Content 자식, Pool 미사용)
- UIPlugInScrollCell — Grid 셀 전용 Pool 마커 (MonoBehaviour, IPoolable)
- ScrollDirection enum (Vertical / Horizontal)
- Scroll Position API (ScrollHeight, ScrollPosition, MaxScrollPosition)

**Excludes (v1):**
- 런타임 Section 추가/삭제
- 가변 높이 셀
- 드래그앤드롭 / 재정렬
- 무한 스크롤 / 페이지네이션

---

## SSOT

### Code Path

```
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/
├── Runtime/Plugins/ScrollSystem/
│   ├── UIComponentScrollContainer.cs    # UIComponentBase 상속
│   ├── UIPlugInScrollGrid.cs
│   ├── UIPlugInScrollFrame.cs
│   └── UIPlugInScrollCell.cs
└── Editor/ScrollSystem/
    └── UIComponentScrollContainerEditor.cs
```

### Implementation Location (3-path mirror)

| 경로 | 역할 |
|------|------|
| `framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Plugins/ScrollSystem/` | UPM (정본) |
| `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/UIPackage/Runtime/Plugins/ScrollSystem/` | Packages (sync) |
| `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/{version}/UIPackage/Runtime/Plugins/ScrollSystem/` | Assets/Samples (import) |

### Prefab Hierarchy

```
SomeCanvas (UICanvas<SomeCanvas>)
└── ScrollContainer ← UIComponentScrollContainer + ScrollRect
    ├── Viewport (RectTransform + Mask)
    └── Content (RectTransform)
        ├── SomeGrid      ← UIPlugInScrollGrid (config holder)
        ├── SomeBanner     ← UIPlugInScrollFrame (고정 표시 요소)
        └── AnotherGrid   ← UIPlugInScrollGrid (config holder)
```

### Class Signatures

#### ScrollDirection

```csharp
namespace Devian
{
    public enum ScrollDirection { Vertical, Horizontal }
}
```

#### UIPlugInScrollCell

```csharp
namespace Devian
{
    [RequireComponent(typeof(RectTransform))]
    public class UIPlugInScrollCell : MonoBehaviour, IPoolable
    {
        public int CellIndex { get; internal set; }
        public RectTransform Rt { get; }

        // IPoolable
        public void OnPoolSpawned();
        public void OnPoolDespawned();
    }
}
```

#### UIPlugInScrollGrid

```csharp
namespace Devian
{
    public class UIPlugInScrollGrid : MonoBehaviour
    {
        // Inspector
        [SerializeField] private string _cellPrefabName;
        [SerializeField] private int _columnCount = 4;
        [SerializeField] private Vector2 _cellSize = new Vector2(200, 200);
        [SerializeField] private Vector2 _spacing = new Vector2(10, 10);

        // Properties
        public string CellPrefabName { get; set; }
        public int ColumnCount { get; set; }
        public Vector2 CellSize { get; set; }
        public Vector2 Spacing { get; set; }
        public int CellCount { get; }

        // Runtime
        public void SetCellCount(int count);

        // Callbacks
        public System.Action<UIPlugInScrollCell, int> onBindCell;
        public System.Action<UIPlugInScrollCell> onUnbindCell;
    }
}
```

#### UIPlugInScrollFrame

```csharp
namespace Devian
{
    public class UIPlugInScrollFrame : MonoBehaviour
    {
        [SerializeField] private float _height = 100f;

        public float Height { get; set; }

        public System.Action<UIPlugInScrollFrame> onBind;
        public System.Action<UIPlugInScrollFrame> onUnbind;
    }
}
```

#### UIComponentScrollContainer

```csharp
namespace Devian
{
    [RequireComponent(typeof(ScrollRect))]
    public class UIComponentScrollContainer : UIComponentBase
    {
        // Inspector
        [SerializeField] private ScrollDirection _direction = ScrollDirection.Vertical;
        [SerializeField] private RectOffset _padding;
        [SerializeField] private float _sectionSpacing = 10f;
        [SerializeField] private int _bufferRows = 2;

        // Scroll Position API
        public bool IsInitialized { get; }
        public float ScrollHeight { get; }
        public float ScrollPosition { get; set; }
        public float MaxScrollPosition { get; }

        // UIComponentBase Lifecycle (내부 자동 호출)
        protected override void onInit();          // ScrollRect 캐시
        protected override void onInitComplete();   // 레이아웃 + 가상화 시작

        // Public API
        public void Refresh();    // 데이터 내용만 변경 시 — 셀 재바인딩
        public void Rebuild();    // 구조 변경 시 — 셀 수 변경, Frame 추가/제거 후
        public void ScrollTo(UIPlugInScrollGrid grid, int localIndex);
        public void ScrollTo(UIPlugInScrollFrame frame);
        public void Clear();
    }
}
```

### Init Sequence (UICanvas 수명주기 통합)

```
UICanvas.Init()
  Phase 1: Component._Init()
    → UIComponentScrollContainer.onInit()
      → ScrollRect 캐시 + Content anchor/pivot 강제 + 이벤트 등록

  Phase 2: Frame._InitFromCanvas()
    → UIGameFrameBag.onInit(canvas)
      → Grid.SetCellCount() + 콜백 설정

  Phase 3: Canvas.onInit()

  Phase 4: Component._InitComplete()
    → UIComponentScrollContainer.onInitComplete()
      → CollectSections → BuildLayoutRows → RecalculateContentSize
      → ApplySectionLayout → 가상화 시작

  Phase 5: Notify(InitOnce)
```

### Grid Cell Lifecycle

```
[뷰포트 진입]
  BundlePool.Spawn<UIPlugInScrollCell>(prefabName)
  → SetParent(Content)
  → anchoredPosition 설정
  → CellIndex = localIndex
  → grid.onBindCell(cell, localIndex)

[뷰포트 이탈]
  grid.onUnbindCell(cell)
  → CellIndex = -1
  → BundlePool.Despawn(cell)
```

### Frame Lifecycle

```
[뷰포트 진입]  frame.gameObject.SetActive(true) → frame.onBind(frame)
[뷰포트 이탈]  frame.onUnbind(frame) → frame.gameObject.SetActive(false)
```

### Refresh vs Rebuild

| | `Refresh()` | `Rebuild()` |
|---|---|---|
| 용도 | 데이터 내용만 변경 | 구조 변경 (셀 수, Frame 유무) |
| 셀 Despawn | 안 함 (재바인딩만) | 전부 Despawn 후 재Spawn |
| 레이아웃 | 유지 | Section 재수집 + 재계산 |
| Content 높이 | 유지 | 재설정 |

```
// 셀 수 변경 후
grid.SetCellCount(newCount);
container.Rebuild();

// Frame 숨김 후
banner.gameObject.SetActive(false);
container.Rebuild();
```

### Editor Support

- **Custom Inspector**: Play 모드에서 Clear / Refresh / Rebuild 버튼 + ScrollHeight/Position 실시간 표시.
- UICanvas.Init()에 의해 자동 초기화 (외부 Init() 호출 불필요).
- **OnDestroy**: Play 모드 종료 시 자동 `Clear()`.

---

## Dependencies

| Dependency | Location |
|------------|----------|
| `BundlePool` | `com.devian.foundation/Samples~/CommonPackage/Runtime/Unity/Pool/Factory/BundlePool.cs` |
| `IPoolable` | `com.devian.foundation/Samples~/CommonPackage/Runtime/Unity/Pool/IPoolable.cs` |
| `ScrollRect` | `UnityEngine.UI` |

---

## Reference

- Parent: `skills/devian-unity/23-ui-package/SKILL.md`
- Pool System: `skills/devian-unity/20-common-package/27-pool-system/SKILL.md`
- Design Plan: `docs/35-ui-plugin-scroll-system-plan.md`
