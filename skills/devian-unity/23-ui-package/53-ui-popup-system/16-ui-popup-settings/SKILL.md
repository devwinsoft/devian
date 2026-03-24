# 16-ui-popup-settings — UISettings Popup Scope

Status: ACTIVE
AppliesTo: v1

## Purpose

popup 전역 설정의 범위를 `UISettings`에 한정한다.

## Code Path

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Base/UISettings.cs
```

## Popup Fields

- `UI_POPUP_CANVAS_ID PopupCanvasId`
- `UIPopupFrameMapEntry[] PopupFrameMappings`
- `Color DimColor`
- `float DimAlpha`

## Rules

- `PopupConfig[]`는 두지 않는다
- popup frame prefab id는 `UISettings.PopupFrameMappings`가 가진다
- popup modal 정책은 `UISettings`가 아니라 frame override 코드가 가진다
- `UIPopupManager.Initialize()`는 `PopupCanvasId`만 읽어 canvas를 bootstrap 한다
- popup prefab resolve는 `PopupFrameMappings`를 사용한다
- editor에서는 `UISettingsEditor`의 `Auto Fill Missing Popup Mappings`로 mapping을 보완할 수 있다
