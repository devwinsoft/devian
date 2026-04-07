# 12-ui-scroll-frame-simple

Status: ACTIVE
AppliesTo: v1

---

## Overview

### Purpose

고정 프리팹 섹션(배너, 헤더, 구분선 등)을 위한 1-row scroll section base.
`UIScrollContainer`는 `IUIScrollSection`으로만 접근하고,
subclass는 `onInit()` / `onShow()` / `onHide()`만 구현하면 된다.

### Scope

**Includes:**
- `UIScrollSimpleFrame` — 상속용 1-row section base

**Excludes:**
- Grid 섹션 → `11-ui-scroll-frame-grid`
- Scroll 엔진 → `10-ui-scroll-container`

---

## SSOT

### Code Path

```
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Scroll/
└── UIScrollSimpleFrame.cs
```

### Class Signature

```csharp
namespace Devian
{
    public class UIScrollSimpleFrame : UIBaseFrame, IUIScrollSection
    {
        protected bool IsShown { get; }

        // subclass hook
        protected override void onInit();
        protected virtual void onShow();
        protected virtual void onHide();
    }
}
```

중요:
- `BindRow()` / `UnbindRow()` / `RefreshRow()` / `ApplySectionLayout()` / `ClearSection()`는
  `IUIScrollSection` explicit interface 구현이다.
- 따라서 subclass와 일반 caller는 raw scroll 메서드를 직접 호출하지 않는다.

---

## Behavior

### Section Contract

- logical row 수는 항상 `1`
- row spacing은 항상 `0`
- row 높이는 `GetHeight()`

### Base Lifecycle

- `onInit()`
  - 일반 `UIBaseFrame` init hook
  - 한번만 호출된다
- `onShow()`
  - row가 보이게 될 때 호출된다
  - base는 이미 `RectTransform` layout과 `SetActive(true)`를 적용한 뒤 호출한다
- `onHide()`
  - row가 숨겨질 때 호출된다
  - refresh에서도 `onHide() -> onShow()` 순서로 호출된다
  - reset/unbind 경계는 `onHide()`다

### Explicit Interface Flow

- `ApplySectionLayout(...)`
  - 초기 숨김
  - `IsShown = false`
- `BindRow(...)`
  - row layout을 `RectTransform`에 적용
  - `SetActive(true)`
  - `IsShown = true`
  - `onShow()`
- `UnbindRow(...)`
  - shown 상태일 때만 `onHide()`
  - `SetActive(false)`
  - `IsShown = false`
- `RefreshRow(...)`
  - shown 상태일 때만 `onHide() -> onShow()`
- `ClearSection()`
  - shown 상태면 `onHide()`
  - `SetActive(false)`
  - `IsShown = false`

### Layout Rule

base가 `UIScrollRowLayout`을 받아 아래를 적용한다.

- anchorMin / anchorMax / pivot = top-left
- vertical:
  - `anchoredPosition = (0, -RowMainAxisPosition)`
  - `sizeDelta = (CrossAxisSize, RowMainAxisSize)`
- horizontal:
  - `anchoredPosition = (RowMainAxisPosition, 0)`
  - `sizeDelta = (RowMainAxisSize, CrossAxisSize)`

subclass는 layout 인자를 직접 받을 필요 없이 `rectTransform` 결과만 사용한다.

### Editor Preview

- `EditorPreviewApplyRowLayout(...)`는 editor preview 전용 internal helper다
- preview에서는 layout만 적용하고 `SetActive(true)` 상태로 보여 준다
- `onShow()`는 호출하지 않는다
- `IsShown = false`를 유지한다

---

## Intended Usage

권장 subclass 패턴:

```csharp
public sealed class UIMyBannerFrame : UIScrollSimpleFrame
{
    protected override void onInit()
    {
        // one-time wiring
    }

    protected override void onShow()
    {
        // current state bind
    }

    protected override void onHide()
    {
        // unbind / reset
    }
}
```

금지/비권장:
- `BindRow()` 직접 호출
- `UnbindRow()` 직접 호출
- scroll state 계산을 subclass가 담당
- `onShow()`를 init처럼 사용하는 것

---

## Dependencies

| Dependency | Location |
|------------|----------|
| `UIBaseFrame` | `Runtime/Base/UIBaseFrame.cs` |
| `IUIScrollSection` | `Runtime/Scroll/IUIScrollSection.cs` |
| `UIUIScrollSectionLayout` | `Runtime/Scroll/UIScrollSectionLayout.cs` |
| `UIScrollRowLayout` | `Runtime/Scroll/UIScrollRowLayout.cs` |

---

## Reference

- Parent: `skills/devian-unity/23-ui-package/SKILL.md`
- ScrollView: `skills/devian-unity/23-ui-package/21-ui-scroll-system/10-ui-scroll-container/SKILL.md`
- Grid section: `skills/devian-unity/23-ui-package/21-ui-scroll-system/11-ui-scroll-frame-grid/SKILL.md`
