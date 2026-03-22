# 18-ui-popup-dim — UIPopupDim

Status: ACTIVE
AppliesTo: v1

## Purpose

popup shared dim / blocker / dim click 처리를 `UIPopupPanel`에서 분리한다.

## Code Path

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Popup/UIPopupDim.cs
```

## Responsibilities

- `Dim` child `GameObject`에 붙는다
- 같은 `GameObject`의 `Image`로 dim color / alpha를 제어한다
- 같은 `GameObject`의 `CanvasGroup`으로 raycast blocking 상태를 제어한다
- `IPointerClickHandler`로 dim click을 받는다
- `UIPopupManager.HandleDimClicked()`로 dim click을 위임한다

## Rules

- `UIPopupDim`은 `PopupRoot`와 분리된 sibling이어야 한다
- `RequireComponent(typeof(Image))`, `RequireComponent(typeof(CanvasGroup))`를 사용한다
- `Button`나 `EventTrigger`를 쓰지 않는다
- dim click 허용 여부는 top popup config 기준이다
- dim 표시 여부와 blocker 여부도 top popup config 기준이다

## Notes

- `UIPopupPanel`은 `UIPopupDim`과 `PopupRoot`만 관리한다
- dim의 실제 visual / click 동작은 `UIPopupDim`이 담당한다
