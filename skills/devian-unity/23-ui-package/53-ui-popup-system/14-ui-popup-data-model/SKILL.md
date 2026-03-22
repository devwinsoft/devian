# 14-ui-popup-data-model

Status: ACTIVE
AppliesTo: v1

## Purpose

popup request / config / result / enum 구조를 정의한다.

## Code Paths

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Popup/PopupRequest.cs
```

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Popup/PopupConfig.cs
```

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Popup/PopupResult.cs
```

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Popup/UIPopupEnums.cs
```

## PopupRequest

- `PopupId`
- `Payload`
- `OnClosed`

runtime에서 config 필드를 override 하지는 않는다.
정책 변경이 필요하면 다른 `PopupId` / `PopupConfig`를 만든다.

## PopupConfig

- `PopupId`
- `UI_POPUP_FRAME_ID PopupFrameId`
- `UseDim`
- `BlockInputBehind`
- `CloseOnBack`
- `CloseOnEscape`
- `CloseOnDimClick`
- `PopupDuplicatePolicy DuplicatePolicy`
- `PlayOpenTransition`
- `PlayCloseTransition`

## PopupResult

- `PopupId`
- `PopupCloseReason`
- `Payload`

close result payload는 frame이 작성하고 manager가 caller callback으로 전달한다.

## Enums

- `PopupDuplicatePolicy`
- `PopupCloseReason`
- `PopupFrameState`
