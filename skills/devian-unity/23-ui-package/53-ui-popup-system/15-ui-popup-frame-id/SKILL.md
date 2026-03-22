# 15-ui-popup-frame-id — UI_POPUP_FRAME_ID

Status: ACTIVE
AppliesTo: v1

## Purpose

`UIPopupFrame` prefab을 선택하기 위한 string wrapper ID.

## Code Paths

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Popup/UI_POPUP_FRAME_ID.cs
```

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Editor/UIPopupFrameIdSelector.cs
```

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Editor/UI_POPUP_FRAME_ID_Drawer.cs
```

## Rules

- selector는 `BaseEditorAssetIdSelector<UIPopupFrame>`를 재사용한다
- UISettings key는 `UI_POPUP_FRAME_ID`
- 권장 SearchDir은 `Assets/Bundles/UIPopupFrames`
- runtime config는 direct prefab reference 대신 `UI_POPUP_FRAME_ID`를 사용한다
