# 22-ui-container-scroll

Status: ACTIVE
AppliesTo: v3

---

## Overview

### Purpose

`UIScrollContainer`는 UIPackage의 **유일한 scroll owner**다.
`ScrollRect` 구독, scroll position / viewport 계산, visible logical row 판정, row enter/exit를 전담한다.

Section(`IUIScrollSection` 구현체)은
container가 계산한 render assignment만 받아 렌더링한다.

### Scope

**Includes:**
- `UIScrollContainer` — `ScrollRect` owner + logical row virtualization 엔진
- `IUIScrollSection` — section과 container 사이의 scroll 전용 계약
- `UIUIScrollSectionLayout` — section 배치 정보 (readonly struct)
- `UIScrollRowLayout` — row 배치 정보 (readonly struct)
- `ScrollDirection` enum
- Editor custom inspector (`Clear / Refresh / Rebuild`)

**Excludes:**
- runtime section 추가/삭제 자동 감지
- variable-height cell virtualization
- drag & drop / reorder
- pagination / infinite scroll

---

## SSOT

### Code Path

```
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/
├── Runtime/Container/
│   ├── UIScrollContainer.cs       # 유일한 scroll owner
│   ├── IUIScrollSection.cs        # scroll 전용 계약
│   ├── UIScrollSectionLayout.cs   # section 배치 정보 (type: UIUIScrollSectionLayout)
│   └── UIScrollRowLayout.cs       # row 배치 정보 (readonly struct)
└── Editor/
    └── UIScrollContainerEditor.cs
```

### Implementation Location (3-path mirror)

| 경로 | 역할 |
|------|------|
| `framework-cs/upm/com.devian.foundation/Samples~/UIPackage/` | UPM (정본) |
| `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/UIPackage/` | Packages (sync) |
| `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/{version}/UIPackage/` | Assets/Samples (import) |

### Prefab Hierarchy

```
SomeCanvas (UICanvas<SomeCanvas>)
└── ScrollContainer ← UIScrollContainer + ScrollRect
    ├── Viewport (RectTransform)
    └── Content (RectTransform)
        ├── SomeGrid    ← UIGridFrame
        ├── SomeBanner  ← UISimpleFrame (24-ui-frame-simple 참조)
        └── AnotherGrid ← UIGridFrame
```

---

## Public API

### ScrollDirection

```csharp
namespace Devian
{
    public enum ScrollDirection { Vertical, Horizontal }
}
```

### IUIScrollSection

```csharp
namespace Devian
{
    public interface IUIScrollSection
    {
        int GetLogicalRowCount();
        float GetLogicalRowMainAxisSize(int localRowIndex);
        float GetLogicalRowSpacing();

        void ApplySectionLayout(in UIUIScrollSectionLayout layout);
        void BindRow(in UIScrollRowLayout rowLayout);
        void UnbindRow(int localRowIndex);
        void RefreshRow(int localRowIndex);
        void ClearSection();
    }
}
```

- section은 scroll 상태를 직접 계산하지 않는다
- container가 계산한 row/section layout만 입력으로 받는다

### UIUIScrollSectionLayout

```csharp
public readonly struct UIUIScrollSectionLayout
{
    public readonly RectTransform Content;
    public readonly ScrollDirection Direction;
    public readonly float SectionMainAxisPosition;
    public readonly float CrossAxisSize;
}
```

### UIScrollRowLayout

```csharp
public readonly struct UIScrollRowLayout
{
    public readonly RectTransform Content;
    public readonly ScrollDirection Direction;
    public readonly int LocalRowIndex;
    public readonly float RowMainAxisPosition;
    public readonly float RowMainAxisSize;
    public readonly float CrossAxisSize;
}
```

### UIScrollContainer

```csharp
namespace Devian
{
    [RequireComponent(typeof(ScrollRect))]
    public class UIScrollContainer : UIBaseContainer
    {
        [SerializeField] private ScrollDirection _direction = ScrollDirection.Vertical;
        [SerializeField] private RectOffset _padding = new RectOffset();
        [SerializeField] private float _sectionSpacing = 10f;
        [SerializeField] private int _bufferRows = 2;

        public bool IsInitialized { get; }
        public float ScrollHeight { get; }
        public float ScrollPosition { get; set; }
        public float MaxScrollPosition { get; }

        public void Refresh();
        public void Rebuild();
        public void ScrollTo(UIBaseFrame frame, int localRowIndex = 0, float offset = 0f);
        public void Clear();
    }
}
```

### Editor

```csharp
namespace Devian.UI.Editor
{
    [CustomEditor(typeof(UIScrollContainer))]
    public class UIScrollContainerEditor : UnityEditor.Editor
    {
    }
}
```

---

## Lifecycle

### UICanvas 통합 순서

`UIScrollContainer`는 직접 `Init()`를 노출하지 않는다.
소유 `UICanvas.Init()` 안에서 초기화된다.

```
UICanvas.Init()
  Phase 1: Container._Init(canvas)
    -> UIScrollContainer.onInit()
       - ScrollRect/content/viewport 캐시
       - Viewport를 parent stretch rect로 정규화
       - Content anchor/pivot/anchoredPosition 정규화
       - ScrollRect.onValueChanged 구독

  Phase 2: UIPanel._InitFromCanvas(owner)

  Phase 3: Canvas.onInit()

  Phase 4: Container._InitComplete() + pending dynamic flush
    -> UIScrollContainer.onInitComplete()
       - Content 하위 UIBaseFrame 수집
       - 각 frame._Init(canvas)
       - IUIScrollSection 수집
       - BuildLogicalRows()
       - content main-axis size 적용
       - ApplySectionLayouts()
       - UpdateVisibleRows()
       - 각 frame._InitComplete()

  Phase 5: UIPanel._InitComplete()
  Phase 6: Canvas.onInitComplete()
  Phase 7: UI_MESSAGE.InitOnce notify
```

동적으로 생성된 `UIScrollContainer`도 `UIPanel.CreateContainer<T>()`를 통해
owner `UICanvas.RegisterDynamicContainerTree()`에 편입되면 동일한 `_Init/_InitComplete`
계약을 따른다.

### Destroy

- `UIScrollContainer`는 `OnDestroy()`를 직접 override하지 않고 `onDestroy()`를 override한다
- base `OnDestroy()`는 non-virtual이며 `Application.isPlaying && !BaseApplication.IsShuttingDown && !BaseApplication.IsApplicationQuitting`일 때만 `onDestroy()`를 호출한다
- 따라서 shutdown / play 종료 상태에서는 `Clear()`가 자동 호출되지 않는다
- 정상 destroy 경로의 `onDestroy()`에서는 `_initialized` 상태일 때 `Clear()`를 호출한다
- `Clear()`는 visible row unbind, section clear, scroll listener 해제, 캐시 초기화를 수행한다

---

## Logical Row Engine

### Section Collection

- `Content` 하위 `UIBaseFrame`를 수집한다
- 그중 `IUIScrollSection` 구현체만 logical row 대상이 된다

### Logical Row Build

container는 section을 직접 보여주지 않고 `logical row` 리스트를 전개한다.

내부 모델:

```csharp
private struct ScrollLogicalRow
{
    public IUIScrollSection Section;
    public UIBaseFrame Frame;
    public int LocalRowIndex;
    public float MainAxisPosition;
    public float MainAxisSize;
    public bool IsVisible;
}
```

동작:
- `GetLogicalRowCount()`만큼 row를 전개
- `GetLogicalRowMainAxisSize()`로 각 row 길이를 계산
- row 간 간격은 `GetLogicalRowSpacing()`
- section 간 간격은 `_sectionSpacing`

### Visibility Update

- 현재 scroll position과 viewport size로 visible row 범위를 계산한다
- `_bufferRows`만큼 앞뒤로 확장한다
- row가 뷰포트에 진입하면 `BindRow(...)`
- row가 뷰포트에서 이탈하면 `UnbindRow(localRowIndex)`

### Render Assignment Rule

section은 raw scroll state를 받지 않는다.
container가 계산한 `UIUIScrollSectionLayout` / `UIScrollRowLayout`만 전달받는다.

---

## Refresh vs Rebuild

| 항목 | `Refresh()` | `Rebuild()` |
|------|-------------|-------------|
| 목적 | 현재 visible logical row 재바인딩 | section/row layout 재계산 |
| row 재전개 | 없음 | 있음 |
| visible row 처리 | `section.RefreshRow(localRowIndex)` | `UnbindRow()` 후 `ClearSection()` |
| section 재수집 | 있음 | 있음 |
| content size 재설정 | 없음 | 있음 |

> 현재 구현의 `CollectSections()`는 `_frames`를 재사용하며, `Content` 하위 frame 자체를 다시 수집하지는 않는다.
> runtime section 추가/삭제 자동 감지는 v3 범위에 포함되지 않는다.

---

## Dependencies

| Dependency | Location |
|------------|----------|
| `UIBaseContainer` | `framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Container/UIBaseContainer.cs` |
| `UIBaseFrame` | `framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Container/UIBaseFrame.cs` |
| `UIGridFrame` | `framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Container/UIGridFrame.cs` |
| `ScrollRect` | `UnityEngine.UI` |

---

## Reference

- Parent: `skills/devian-unity/23-ui-package/SKILL.md`
- Grid section: `skills/devian-unity/23-ui-package/23-ui-frame-grid/SKILL.md`
- Canvas system: `skills/devian-unity/23-ui-package/11-ui-canvas-system/SKILL.md`
