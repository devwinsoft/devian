# 12-ui-scroll-frame-simple

Status: ACTIVE
AppliesTo: v1

---

## Overview

### Purpose

고정 프리팹 섹션 (배너, 헤더, 구분선 등).
UIBaseFrame + IUIScrollSection 구현. Logical row 1개를 차지한다.

### Scope

**Includes:**
- UIScrollSimpleFrame — 1-row section (UIBaseFrame, IUIScrollSection)

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
        public Action<UIScrollSimpleFrame> onBind;
        public Action<UIScrollSimpleFrame> onUnbind;

        // IUIScrollSection
        int GetLogicalRowCount();              // 항상 1
        float GetLogicalRowMainAxisSize(int);  // GetHeight()
        float GetLogicalRowSpacing();           // 0

        void ApplySectionLayout(in UIUIScrollSectionLayout layout);
        void BindRow(in UIScrollRowLayout rowLayout);
        void UnbindRow(int localRowIndex);
        void RefreshRow(int localRowIndex);
        void ClearSection();
    }
}
```

### Behavior

- `GetLogicalRowCount()` = 1
- `ApplySectionLayout()`: 초기 숨김 (SetActive(false))
- `BindRow()`: anchor/pivot 설정 → 위치/크기 적용 → SetActive(true) → onBind
- `UnbindRow()`: onUnbind → SetActive(false)
- `RefreshRow()`: onUnbind → onBind
- `ClearSection()`: onUnbind → SetActive(false)
- 높이는 RectTransform에서 가져온다 (Inspector에서 크기 설정)

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
