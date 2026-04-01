# 11-ui-popup-canvas-panel — UIPopupCanvas / UIPopupPanel

Status: ACTIVE
AppliesTo: v1

## Purpose

popup overlay canvas와 panel layer root를 정의한다.

## Implementation Location (3-path mirror)

| 경로 | 역할 |
|------|------|
| `framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Popup/` | UPM mirror |
| `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/UIPackage/Runtime/Popup/` | Packages mirror |
| `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/UIPackage/Runtime/Popup/` | 현재 workspace 구현 기준 |

## UIPopupCanvas

- `UIBaseCanvas<UIPopupCanvas>, IPoolable`
- `UIPopupPanel _panel`을 소유한다
- `NormalizeCanvasRect()`로 full-stretch rect를 강제한다
- bootstrap 후 `Init()`되어 `panel`을 expose한다
- public `OnPoolSpawned()` / `OnPoolDespawned()`는 base `_HandlePoolSpawned()` / `_HandlePoolDespawned()` bridge다
- canvas/panel은 init once를 유지하고, respawn 시 base helper가 canvas-owned component와 panel-owned subtree를 재초기화한다

## UIPopupPanel

- `UIBasePanel<UIPopupCanvas>`
- `UIPopupDim`과 `PopupRoot`를 소유한다
- manager가 spawn한 `UIPopupFrameBase`를 `PopupRoot`에 attach / detach 한다
- `AttachFrame(frame)`는 frame을 `PopupRoot`에 붙인 뒤 아직 init 전이면 `frame._Init(owner.canvas)` + `frame._InitComplete()`를 호출한다
- panel 아래 시각 계층은 다음을 기준으로 정리한다

```text
Dim (UIPopupDim)
PopupRoot
  -> popup frames...
```

## UIPopupDim

- `Dim` child `GameObject`에 붙는 shared modal layer component
- 같은 `GameObject`의 `Image`와 `CanvasGroup`를 사용한다
- `RequireComponent(typeof(Image))`, `RequireComponent(typeof(CanvasGroup))` 규약으로 고정한다
- `IPointerClickHandler`로 dim click을 manager에 전달한다
- `Button` / `EventTrigger`는 사용하지 않는다

## Modal State

`ApplyModalState(bool showDim, bool blockBehind, bool closeOnDimClick)`가 담당한다.

- dim 표시 여부
- blocker 여부
- dim click 허용 여부

panel은 `UIPopupDim.ApplyState(...)`로 위 상태를 위임한다.
dim color/alpha는 prefab Image의 원본 값을 사용한다 (스크립트에서 덮어쓰지 않음).
dim과 blocker는 항상 top popup frame policy 기준으로 계산한다.

## Input Gate Boundary

- shared dim/blocker는 `UIPopupDim`이 담당한다
- popup 간 top-only input gate는 `UIPopupFrameBase`의 root `CanvasGroup`가 담당한다
- `UIPopupPanel`은 popup별 input gate를 직접 관리하지 않는다

## Editor Install

- `UIPopupPanelEditor`가 Edit Mode inspector button을 제공한다
- `Install Missing`은 `Dim`과 `PopupRoot` child를 생성하고 reference를 연결한다
- `Dim`에는 `Image`, `CanvasGroup`, `UIPopupDim`을 설치한다
- `Normalize Layout`는 두 child rect를 full-stretch로 다시 맞춘다
