# 20-ui-component-circle-filter

Status: ACTIVE
AppliesTo: v11

## Purpose

Circle/Collider2D-based raycast filter for UI elements.
Filters UI raycasts based on whether the point is inside the attached `Collider2D`.

## Scope

### Includes
- `ICanvasRaycastFilter` 구현
- `Collider2D` 기반 히트 테스트
- `ScreenPointToWorldPointInRectangle` 변환

### Excludes
- 비-`Collider2D` 필터링
- 자동 collider 생성

## Requirements

- `Collider2D` (`RequireComponent`)
- `RectTransform` (`RequireComponent`)

## SSOT

### Code Path
```
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Component/UIComponentCircleFilter.cs
```

### Class
```csharp
namespace Devian
{
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(RectTransform))]
    public class UIComponentCircleFilter : MonoBehaviour, ICanvasRaycastFilter
}
```

## Reference

- Parent: `../00-overview/SKILL.md`
- Index: `skills/devian-unity/23-ui-package/SKILL.md`
