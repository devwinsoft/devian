# 13-ui-popup-frame — UIPopupFrame

Status: ACTIVE
AppliesTo: v1

## Purpose

popup 1건의 실제 표시 단위.
request bind, open/close transition, close result, top-state input 제어를 담당한다.

## Code Path

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Popup/UIPopupFrame.cs
```

## Shape

```csharp
[RequireComponent(typeof(UITransitionPlayer))]
[RequireComponent(typeof(CanvasGroup))]
public class UIPopupFrame : UIBaseFrame, IPoolable
{
    public PopupFrameState state { get; }
    public bool isTop { get; }

    internal void Open(...);
    internal void CloseFromManager(PopupCloseReason reason, object payload = null);
    internal void SetTopState(bool isTop, bool allowInput);

    public void CloseCompleted();
    public void CloseCanceled();
    protected void CloseWithResult(PopupCloseReason reason, object payload = null);
}
```

## State Machine

- `Opening`
- `Opened`
- `Closing`

규칙:

- `Opening` 중 close 요청이 오면 바로 `Closing`
- `Closing` 중 추가 close 요청은 무시
- close 완료 시점에만 manager callback

## Transition

- same-GO `UITransitionPlayer` 사용
- `_openTransitionId`, `_closeTransitionId`를 frame이 직접 소유
- `PopupConfig.PlayOpenTransition`, `PopupConfig.PlayCloseTransition`으로 실제 재생 여부 결정

## Top-State

- `SetTopState(isTop, allowInput)`가 top 여부와 input 가능 여부를 분리한다
- input gating은 frame root same-`GameObject` `CanvasGroup`로 처리한다
- input gate용 serialized ref를 두지 않는다
- top이 아니거나 `Opened`가 아니면 input 비활성
