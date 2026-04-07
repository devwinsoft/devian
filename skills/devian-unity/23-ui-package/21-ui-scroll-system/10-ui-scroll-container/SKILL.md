# 10-ui-scroll-container

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
- `UIScrollDirection` enum
- Editor custom inspector (`Auto Preview / Preview Layout / Clear Preview / Clear / Refresh / Rebuild`)
- edit mode layout-only preview

**Excludes:**
- runtime section 추가/삭제 자동 감지
- variable-height cell virtualization
- edit mode pooled cell spawn preview
- drag & drop / reorder
- pagination / infinite scroll

---

## SSOT

### Code Path

```
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/
├── Runtime/Scroll/
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
SomeCanvas (UIBaseCanvas<SomeCanvas>)
└── ScrollContainer ← UIScrollContainer + ScrollRect
    ├── Viewport (RectTransform)
    └── Content (RectTransform)
        ├── SomeGrid    ← UIScrollGridFrame
        ├── SomeBanner  ← UIScrollSimpleFrame (12-ui-scroll-frame-simple 참조)
        └── AnotherGrid ← UIScrollGridFrame
```

---

## Public API

### UIScrollDirection

```csharp
namespace Devian
{
    public enum UIScrollDirection { Vertical, Horizontal }
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
    public readonly UIScrollDirection Direction;
    public readonly float SectionMainAxisPosition;
    public readonly float CrossAxisSize;
}
```

### UIScrollRowLayout

```csharp
public readonly struct UIScrollRowLayout
{
    public readonly RectTransform Content;
    public readonly UIScrollDirection Direction;
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
        [SerializeField] private UIScrollDirection _direction = UIScrollDirection.Vertical;
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
namespace Devian
{
    [CustomEditor(typeof(UIScrollContainer))]
    public class UIScrollContainerEditor : UnityEditor.Editor
    {
    }
}
```

edit mode preview API:

```csharp
public void EditorRequestPreviewRebuild();
public void EditorPreviewRebuildLayout();
public void EditorClearPreview();
```

---

## Lifecycle

### UIBaseCanvas 통합 순서

`UIScrollContainer`는 직접 `Init()`를 노출하지 않는다.
소유 panel의 lifecycle을 통해 `UIBaseCanvas.Init()` 안에서 초기화된다.

```
UIBaseCanvas.Init()
  Phase 1: Canvas-owned component init

  Phase 2: UIBasePanel._InitFromCanvas(owner)
    -> panel이 subtree를 스캔
    -> UIScrollContainer._Init(canvas)
       -> UIScrollContainer.onInit()
          - ScrollRect/content/viewport 캐시
          - Viewport를 parent stretch rect로 정규화
          - Content anchor/pivot/anchoredPosition 정규화
          - ScrollRect.onValueChanged 구독

  Phase 3: Canvas.onInit()

  Phase 4: UIBasePanel._InitComplete()
    -> panel 소유 container foreach: container._InitComplete()
       -> UIScrollContainer.onInitComplete()
          - Content 하위 UIBaseFrame 수집
          - 각 frame._Init(canvas)
          - IUIScrollSection 수집
          - BuildLogicalRows()
          - content main-axis size 적용
          - ApplySectionLayouts()
          - _initialized = true
          - UpdateVisibleRows()
          - 각 frame._InitComplete()

  Phase 5: Canvas.onInitComplete()
  Phase 6: UI_MESSAGE.InitOnce notify
```

동적으로 생성된 `UIScrollContainer`도 `UIBasePanel.CreateContainer<T>()`를 통해
owner panel lifecycle에 편입되면 동일한 `_Init/_InitComplete`
계약을 따른다. panel init complete 이후 생성되면 `_InitComplete()`가 즉시 호출된다.

### Destroy

- `UIScrollContainer`는 `OnDestroy()`를 직접 override하지 않고 `onDestroy()`를 override한다
- base `OnDestroy()`는 non-virtual이며 `Application.isPlaying && !BaseApplication.IsShuttingDown && !BaseApplication.IsApplicationQuitting`일 때만 `onDestroy()`를 호출한다
- 따라서 shutdown / play 종료 상태에서는 `Clear()`가 자동 호출되지 않는다
- 정상 destroy 경로의 `onDestroy()`에서는 `_initialized` 상태일 때 `Clear()`를 호출한다
- `Clear()`는 visible row unbind, section clear, scroll listener 해제, 캐시 초기화를 수행한다

### Edit Mode Preview

- edit mode preview는 runtime `Init()`를 재사용하지 않는다
- preview는 `ScrollRect/content/viewport` 캐시, frame/section 수집, logical row 계산, content size 반영, section layout 적용까지만 수행한다
- `UpdateVisibleRows()`, `BindRow()`, `BundlePool.Spawn()`은 호출하지 않는다
- `UIScrollSimpleFrame`만 editor helper로 위치/크기를 직접 반영한다
- `UIScrollGridFrame`은 section transform + height만 확인한다
- `Auto Preview`가 켜져 있으면 `OnValidate()` 변경을 debounce 후 재계산한다
- preview가 이미 활성화된 상태에서 child frame의 main-axis size를 수정하면, 다음 rebuild 전에 그 최신 size를 baseline에 반영한 뒤 preview를 다시 계산한다
- `Clear Preview`는 preview 적용 전 baseline rect 상태를 복원한다

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
- `UIScrollSimpleFrame`의 경우 위 메서드들은 explicit interface 구현이므로 container만 호출한다. subclass는 `onShow()` / `onHide()`만 구현한다.

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
| frame/section 재수집 | 없음 | 있음 |
| content size 재설정 | 없음 | 있음 |

> `Rebuild()`는 `Content` 하위 frame을 다시 수집한 뒤 section/row layout을 재계산한다.
> runtime section 추가/삭제 자동 감지는 여전히 v3 범위 밖이다.

---

## Dependencies

| Dependency | Location |
|------------|----------|
| `UIBaseContainer` | `framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Base/UIBaseContainer.cs` |
| `UIBaseFrame` | `framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Base/UIBaseFrame.cs` |
| `UIScrollGridFrame` | `framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Scroll/UIScrollGridFrame.cs` |
| `ScrollRect` | `UnityEngine.UI` |

---

## Reference

- Parent: `skills/devian-unity/23-ui-package/SKILL.md`
- Grid section: `skills/devian-unity/23-ui-package/21-ui-scroll-system/11-ui-scroll-frame-grid/SKILL.md`
- Simple section: `skills/devian-unity/23-ui-package/21-ui-scroll-system/12-ui-scroll-frame-simple/SKILL.md`
- Canvas system: `skills/devian-unity/23-ui-package/10-base-system/11-ui-canvas-system/SKILL.md`
