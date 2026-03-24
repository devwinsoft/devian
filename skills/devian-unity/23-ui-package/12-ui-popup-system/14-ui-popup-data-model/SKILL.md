# 14-ui-popup-data-model

Status: ACTIVE
AppliesTo: v1

## Purpose

popup enum / frame-map entry / settings boundary를 정의한다.

## Code Paths

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Popup/UIPopupEnums.cs
```

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Popup/UIPopupFrameMapEntry.cs
```

## Show / Close Payload

- `UIPopupManager.Show<TFrame>(...)`는 frame type과 payload를 직접 받는다.
- close callback은 `PopupCloseReason`만 caller로 전달한다.

## Popup Frame Mapping

- popup prefab id는 `UISettings.PopupFrameMappings`에 저장한다
- 각 entry는 `FrameTypeName`과 `UI_POPUP_FRAME_ID`를 가진다
- runtime resolve는 `UIPopupManager`가 담당한다

## Enums

- `PopupDuplicatePolicy`
- `PopupCloseReason`
- `PopupFrameState`
