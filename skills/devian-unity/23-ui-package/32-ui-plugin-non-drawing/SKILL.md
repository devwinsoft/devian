# 32-ui-plugin-non-drawing

Status: ACTIVE
AppliesTo: v11

## Purpose

A `Graphic` that does not draw anything.
Useful for raycast targets or layout participation without visual rendering.

## Scope

### Includes
- `Graphic` 상속
- `SetMaterialDirty()` / `SetVerticesDirty()` no-op
- `OnPopulateMesh()`에서 `VertexHelper.Clear()`

### Excludes
- 실제 시각 렌더링

## SSOT

### Code Path
```
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Plugins/UIPlugInNonDrawing.cs
```

### Class
```csharp
namespace Devian
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class UIPlugInNonDrawing : Graphic
}
```

## Reference

- Parent: `skills/devian-unity/23-ui-package/SKILL.md`
