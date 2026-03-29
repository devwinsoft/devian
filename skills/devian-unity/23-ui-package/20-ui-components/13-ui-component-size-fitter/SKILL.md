# 13-ui-component-size-fitter

Status: ACTIVE
AppliesTo: v11

## Purpose

`UIComponentBaseSizeFitter` 계열은 `RectTransform` 크기/레이아웃을 화면 기준으로 자동 보정하는
`ExecuteAlways` UI 컴포넌트 묶음이다.

이 스킬은 아래 2개를 통합한다.

- `UIComponentSafeSizeFitter` — safe area 기준 anchor/offset 보정
- `UIComponentCanvasSizeFitter` — `Image`를 부모/canvas 영역에 비율 유지로 맞춤

같은 `GameObject`에 size-fitter 계열을 2개 이상 붙이는 것은 지원하지 않는다.
조합이 필요하면 `parent = safe`, `child = canvas/image fit` 계층으로 분리한다.

## Scope

### Includes
- `UIComponentBaseSizeFitter : UIComponentBase`
- baseline layout capture / restore
- disable / domain reload 시 baseline 복원
- `OnEnable` / parent 변경 / 해상도 변경 / orientation 변경 refresh
- world-space canvas no-op
- same-object size-fitter 중복 guard
- `UIComponentSafeSizeFitter`
- `UIComponentCanvasSizeFitter`

### Excludes
- `LayoutGroup`과의 우선순위 조정
- 같은 `GameObject`에서 safe fit + canvas fit 동시 적용
- image crop / mask / shader 보정
- device-specific safe area override

## SSOT

### Code Path

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Component/
├── UIComponentBase.cs
├── UIComponentBaseSizeFitter.cs
├── UIComponentSafeSizeFitter.cs
└── UIComponentCanvasSizeFitter.cs
```

### Classes

```csharp
namespace Devian
{
    public abstract class UIComponentBaseSizeFitter : UIComponentBase
    {
        public RectTransform Target { get; }
        public void Refresh();
    }

    public sealed class UIComponentSafeSizeFitter : UIComponentBaseSizeFitter
    {
        public enum SafeAreaApplyMode
        {
            Anchor,
            Offset,
        }
    }

    public sealed class UIComponentCanvasSizeFitter : UIComponentBaseSizeFitter
    {
        public enum CanvasSizeFitMode
        {
            Inner,
            Outer,
        }
    }
}
```

## Common Base

### Responsibilities

- `UIComponentBase` lifecycle 통합
- baseline capture / restore
- `Refresh()` 진입 전 baseline 복원 후 재계산
- `WorldSpace` canvas early return
- editor domain reload 전 baseline 복원
- same-object 중복 size-fitter warning

### Serialized Base Fields

```csharp
[SerializeField] private bool _refreshOnEnable = true;
[SerializeField] private bool _refreshOnResolutionChange = true;
[SerializeField] private bool _refreshOnOrientationChange = true;

[SerializeField, HideInInspector] private RectTransform _baselineTarget;
[SerializeField, HideInInspector] private Vector2 _baselineAnchorMin;
[SerializeField, HideInInspector] private Vector2 _baselineAnchorMax;
[SerializeField, HideInInspector] private Vector2 _baselineOffsetMin;
[SerializeField, HideInInspector] private Vector2 _baselineOffsetMax;
[SerializeField, HideInInspector] private Vector2 _baselineSizeDelta;
[SerializeField, HideInInspector] private bool _hasBaseline;
```

## UIComponentSafeSizeFitter

### Behavior

- target은 self `RectTransform`
- safe area source:
  - runtime: `Screen.safeArea`
  - editor: simulation profile
- `Anchor`:
  - safe rect를 0..1 anchor로 변환
  - baseline offset 유지
- `Offset`:
  - baseline anchor 유지
  - safe inset을 `offsetMin/offsetMax`에 반영
- 마지막 적용 상태:
  - `LastAppliedSafeArea`
  - `LastOrientation`
  - `IsApplied`

### Policy

- 전체 safe root, HUD bar, 화면 모서리 버튼 루트에 사용
- 배경 이미지 stretch 용도에는 직접 쓰지 않는다

## UIComponentCanvasSizeFitter

### Required Component

- `Image` (`RequireComponent(typeof(Image))`)

### Behavior

- source image:
  - `overrideSprite` 우선
  - 없으면 `sprite`
- fitting area:
  - 우선 parent `RectTransform.rect.size`
  - 없으면 root canvas rect
- sprite 비율을 유지한다
- `Inner`:
  - contain
  - fitting area 안에 완전히 들어오면서 가장 크게 맞춤
- `Outer`:
  - cover
  - fitting area 전체를 덮으면서 가장 작게 맞춤
- `RectTransform.SetSizeWithCurrentAnchors()`로 width/height만 반영한다

### Policy

- fullscreen/background image, splash image, banner image에 사용
- safe area와 같이 써야 하면 parent에 `UIComponentSafeSizeFitter`, child image에 `UIComponentCanvasSizeFitter`를 둔다
- 같은 `GameObject`에 2개 이상 size-fitter를 붙이지 않는다

## Naming

- 사용자 요구의 `outter`는 오탈자로 보고 enum 값은 `Outer`를 사용한다.
- `UIComponentCanvasSizeFitter`는 raw screen pixel이 아니라 canvas-space fitting을 의미한다.

## Reference

- Parent: [00-overview](../00-overview/SKILL.md)
- Base: [10-ui-component-base](../10-ui-component-base/SKILL.md)
- Debug: [UIComponentSafeSizeFitter_Debug_Guide](./UIComponentSafeSizeFitter_Debug_Guide.md)
