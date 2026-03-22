# 17-ui-popup-canvas-id — UI_POPUP_CANVAS_ID

Status: ACTIVE
AppliesTo: v1

## Purpose

`UIPopupCanvas` prefab을 선택하기 위한 string wrapper ID.
`MobileApplication` bootstrap 시 popup canvas spawn에 사용한다.

## Code Paths

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Popup/UI_POPUP_CANVAS_ID.cs
```

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Editor/UIPopupCanvasIdSelector.cs
```

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Editor/UI_POPUP_CANVAS_ID_Drawer.cs
```

## Rules

- selector는 `BaseEditorAssetIdSelector<UIPopupCanvas>`를 재사용한다
- UISettings key는 `UI_POPUP_CANVAS_ID`
- 권장 SearchDir은 `Assets/Bundles/UIPopupCanvases`
- `UISettings.PopupCanvasId`가 이 타입을 사용한다
