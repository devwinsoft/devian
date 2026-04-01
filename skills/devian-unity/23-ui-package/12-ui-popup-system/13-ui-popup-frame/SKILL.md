# 13-ui-popup-frame — UIPopupFrameBase

Status: ACTIVE
AppliesTo: v1

## Purpose

popup 1건의 실제 표시 단위.
frame policy, request bind, show/close transition, close reason, top-state input 제어를 담당한다.

## Implementation Location (3-path mirror)

| 경로 | 역할 |
|------|------|
| `framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Popup/UIPopupFrameBase.cs` | UPM mirror |
| `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/UIPackage/Runtime/Popup/UIPopupFrameBase.cs` | Packages mirror |
| `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/UIPackage/Runtime/Popup/UIPopupFrameBase.cs` | 현재 workspace 구현 기준 |

## Shape

```csharp
public abstract class UIPopupFrameBase : UIBaseFrame, IPoolable
{
    public PopupFrameState State { get; }
    public bool IsTop { get; }

    public void OnPoolSpawned();
    public void OnPoolDespawned();

    internal void ShowUntyped(...);
    internal void CloseFromManager(PopupCloseReason reason);
    internal void SetTopState(bool isTop, bool allowInput);

    public void CloseCompleted();
    public void CloseCanceled();
    protected virtual void ClosePopup(PopupCloseReason reason = PopupCloseReason.Completed);
}
```

```csharp
public abstract class UIPopupFrameBase<TReq> : UIPopupFrameBase
{
    protected TReq CurrentRequest { get; }
    protected abstract void onBind(TReq request);
}
```

기본 concrete frame은 두지 않는다.
실제 popup은 `UIPopupFrameBase` 또는 `UIPopupFrameBase<TReq>`를 상속한 concrete class로 직접 만든다.

## State Machine

- `Showing`
- `Show`
- `Closing`

규칙:

- `Showing` 중 close 요청이 오면 바로 `Closing`
- `Closing` 중 추가 close 요청은 무시
- close 완료 시점에만 manager callback

## Transition

- same-GO `UITransitionPlayer` 사용
- `_showTransitionId`, `_closeTransitionId`를 frame이 직접 소유
- 실제 재생 여부는 frame의 `PlayShowTransition`, `PlayCloseTransition` override와 preset id 유효성으로 결정

## Pool Contract

- public `OnPoolSpawned()` / `OnPoolDespawned()`는 base frame pool handler bridge다.
- `onPoolSpawned()`와 `onPoolDespawned()`는 transition cancel, payload/top-state reset을 담당한다.
- base `UIBaseFrame`가 despawn 시 init state를 reset하므로, respawn 뒤 panel attach 경로에서 `_Init()` / `_InitComplete()`가 다시 호출될 수 있다.

## Top-State

- `SetTopState(isTop, allowInput)`가 top 여부와 input 가능 여부를 분리한다
- input gating은 frame root same-`GameObject` `CanvasGroup`로 처리한다
- input gate용 serialized ref를 두지 않는다
- top이 아니거나 `Show`가 아니면 input 비활성

## Policy Ownership

popup modal 정책은 frame 코드 override로 정의한다.

- `UseDim`
- `BlockInputBehind`
- `CloseOnBack`
- `CloseOnEscape`
- `CloseOnDimClick`
- `DuplicatePolicy`
