# 32-ui-plugin-non-drawing

Status: ACTIVE
AppliesTo: v10

## Purpose

A Graphic that does not draw anything.
Useful for raycast targets or layout purposes without visual rendering.

## Scope

### Includes
- Graphic 상속 (raycastTarget, layout 참여 가능)
- SetMaterialDirty/SetVerticesDirty no-op
- OnPopulateMesh clear (렌더링 없음)

### Excludes
- 실제 시각적 렌더링

## SSOT

### Code Path
```
framework-cs/upm/com.devian.foundation/Runtime/Unity/UI/Plugins/UIPlugInNonDrawing.cs
```

### Class
```csharp
namespace Devian
{
    public class UIPlugInNonDrawing : Graphic
}
```

## Reference

- Parent: `skills/devian-unity/40-ui-components/skill.md`
