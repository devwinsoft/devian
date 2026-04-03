# 14-ui-transition-player

Status: ACTIVE
AppliesTo: v2
Type: Runtime Specification

## Purpose

`UITransitionPlayer`는 compiled transition result를 실제 UI 대상에 적용하는 executor다.

## Target Code Path

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Tween/UITransitionPlayer.cs
```

## Target References

- same-GameObject `RectTransform` (required)
- same-GameObject `CanvasGroup` (optional)
- same-GameObject `LayoutElement` (optional)

## Minimum API

```csharp
public sealed class UITransitionPlayer : MonoBehaviour
{
    public UITweenHandle Play(UITransitionPreset preset, Action onComplete = null);
    public UITweenHandle Play(UITransitionPresetAsset asset, Action onComplete = null);
    public UITweenHandle Play(UI_TRANSITION_PRESET_ID id, Action onComplete = null);
    public UITweenHandle Play(UITweenSequence sequence, Action onComplete = null);
    public void Cancel();
}
```

## Rules

- player는 main transition 채널 1개만 가진다
- 새 `Play(...)` 호출 시 기존 main transition은 cancel된다
- `RectTransform`는 `[RequireComponent(typeof(RectTransform))]`로 같은 GameObject에 필수다
- alpha는 같은 GameObject의 `CanvasGroup`에 적용한다
- anchoredPosition은 같은 GameObject의 `RectTransform`에 적용한다
- scale은 같은 GameObject의 `transform.localScale`에 적용한다
- preferred size는 같은 GameObject의 `LayoutElement.preferredWidth/preferredHeight`에 적용한다
- `Play(...)`는 preset 또는 sequence를 `UICompiledTransitionData`로 compile한 뒤 runner에 위임한다
- play 시작 시 `CaptureSnapshot()`으로 alpha / scale과 anchoredPosition baseline을 캡처한다
- 매 프레임 `UITransitionFrameResult`만 적용한다
- move 결과는 player가 보관한 anchoredPosition baseline 기준 offset으로 계산된다
- preferred size 결과는 absolute `preferredWidth/preferredHeight` 값으로 적용된다
- `CanvasGroup`가 없고 preset이 alpha를 요구하면 alpha 채널만 경고 후 skip 한다
- `LayoutElement`가 없고 preset이 preferred size를 요구하면 preferred size 채널만 경고 후 skip 한다
- `Play(UI_TRANSITION_PRESET_ID id)`는 editor play mode에서 raw preset asset을 우선 resolve한다
- bundle build/runtime에서는 cached `UITransitionPresetAsset`을 사용한다
- bundle/runtime 경로를 쓰기 전에는 preset asset bundle이 선로드되어 있어야 한다
- 현재 기본 bootstrap은 `MobileApplication.onLoadCompletedAsync()`에서 `ui` label의 `UITransitionPresetAsset`을 preload한다
- `Reset/Awake/OnValidate`에서 same-GameObject target 참조를 보정한다
- `Reset/Awake/OnValidate`에서 anchoredPosition baseline도 갱신한다
- 외부 코드가 baseline 자체를 바꿨다면 `RefreshBaseline()`으로 수동 동기화할 수 있다
- `OnDestroy()`에서는 현재 main handle을 cancel한다
- player는 preset 의미를 소유하지 않는다. 어떤 preset을 재생할지는 owner가 결정한다

## Attachment Guidance

- `UITransitionPlayer`는 `LayoutRoot`보다 내부 `VisualRoot`에 붙이는 것을 우선한다
- `UITransitionPlayer`는 tween 대상과 같은 GameObject에 붙인다
- layout과 함께 폭/높이를 바꾸려면 `LayoutElement.preferredWidth/preferredHeight` 채널을 사용한다
- `UIBasePanel`은 base `Show()` / `Hide()` 또는 concrete panel API에서 player를 호출할 수 있다
- `UIBaseContainer`는 non-layout visual root에만 tween을 적용한다
- `UIBaseFrame`가 기본 적용 지점이다
- item add / reward gain / badge update 같은 game event에서는 owner component가 manual `Play(...)`를 직접 호출한다
- pooled object는 despawn 전 `Cancel()`을 호출하는 경로를 가져야 한다
- `UIScrollContainer` virtualized item의 hide tween에는 사용하지 않는다

## Responsibility Boundary

- `UIBasePanel.Show()` / `UIBasePanel.Hide()`, `Container` / `Frame`의 명시적 API, 또는 game event handler가 transition 타이밍을 결정한다
- 어떤 preset을 쓸지는 `UITransitionPlayer`가 아니라 owner(`UIToastFrame`, concrete panel 등)가 결정한다
- `UITransitionPlayer`는 실행만 담당한다
- queue, state, duplicate 판단은 player 책임이 아니다
