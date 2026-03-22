# 21-ui-container-simple

Status: ACTIVE
AppliesTo: v1

---

## Overview

### Purpose

최소 container 구현체. ScrollRect/가상화/레이아웃 없이
UIBaseFrame subtree만 초기화한다.

### Scope

**Includes:**
- UISimpleContainer — UIBaseContainer 상속, frame subtree bootstrap
- UIPanel.CreateContainer<UISimpleContainer>()로 동적 생성 지원

**Excludes:**
- ScrollRect 구독
- viewport 계산 / logical row / virtualization
- nested UIBaseContainer
- Refresh / Rebuild

---

## SSOT

### Code Path

```
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Container/
└── UISimpleContainer.cs
```

### Class Signature

```csharp
namespace Devian
{
    public class UISimpleContainer : UIBaseContainer
    {
        public bool IsInitialized { get; }
        public void Clear();
    }
}
```

### Behavior

- `onInit()`: 아무 일도 하지 않음 (base 기본)
- `onInitComplete()`:
  1. GetComponentsInChildren<UIBaseFrame>(true) 수집
  2. 각 frame._Init(canvas)
  3. 각 frame._InitComplete()
  4. _initialized = true
- `Clear()`: frame._Clear() → _frames.Clear() → _initialized = false
- `onDestroy()`: `_initialized`이면 `Clear()`

Destroy 규약:

- base `OnDestroy()`는 non-virtual
- 실제 정리 로직은 `onDestroy()` override에 둔다
- shutdown / play 종료 상태에서는 `onDestroy()`가 호출되지 않는다

### Constraints

- Nested container 비지원 (subtree에 다른 UIBaseContainer 두지 않음)
- Frame 수집은 전체 subtree (GetComponentsInChildren)

---

## Dependencies

| Dependency | Location |
|------------|----------|
| `UIBaseContainer` | `Runtime/Container/UIBaseContainer.cs` |
| `UIBaseFrame` | `Runtime/Container/UIBaseFrame.cs` |

---

## Reference

- Parent: `skills/devian-unity/23-ui-package/SKILL.md`
- Canvas System: `skills/devian-unity/23-ui-package/11-ui-canvas-system/SKILL.md`
