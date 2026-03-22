# 16-ui-toast-canvas-id — UI_TOAST_CANVAS_ID

Status: ACTIVE
AppliesTo: v1

## Purpose

`UIToastCanvas` prefab을 선택하기 위한 string wrapper ID.
`UIToastService.Initialize()` 시 toast canvas spawn에 사용한다.

## Code Paths

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Toast/UI_TOAST_CANVAS_ID.cs
```

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Editor/UIToastCanvasIdSelector.cs
```

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Editor/UI_TOAST_CANVAS_ID_Drawer.cs
```

## Rules

- selector는 `BaseEditorUIAssetIdSelector<UIToastCanvas>`를 사용한다
- UISettings key는 `UI_TOAST_CANVAS_ID`
- 권장 SearchDir은 `Assets/Bundles/UI/Prefabs`
- `UISettings.ToastCanvasId`가 이 타입을 사용한다
