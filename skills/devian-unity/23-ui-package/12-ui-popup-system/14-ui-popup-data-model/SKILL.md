# 14-ui-popup-data-model

Status: ACTIVE
AppliesTo: v1

## Purpose

popup enum / popup frame id / settings boundary를 정의한다.
popup show는 caller가 `UI_POPUP_FRAME_ID`를 직접 넘기고, manager가 frame prefab의 실제 component type을 resolve한다.

## Code Paths

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Popup/UIPopupEnums.cs
```

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Popup/UI_POPUP_FRAME_ID.cs
```

## Show / Close Callback

- `UIPopupManager.Show(UI_POPUP_FRAME_ID, ...)`는 frame prefab id와 close callback을 직접 받는다.
- close callback은 `PopupCloseReason`만 caller로 전달한다.

## Enums

- `PopupDuplicatePolicy`
- `PopupCloseReason` — `Confirm` / `Yes` / `No` / `Cancel`
  - `Confirm`: 단일 확인 버튼 (OK / 확인)
  - `Yes`: 2-way 선택에서 긍정 (Yes)
  - `No`: 2-way 선택에서 부정 (No)
  - `Cancel`: Back / Escape / Dim click / Replace / 강제 종료 등 암묵적 기각
- `PopupFrameState`
