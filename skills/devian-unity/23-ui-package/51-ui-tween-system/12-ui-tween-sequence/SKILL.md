# 12-ui-tween-sequence

Status: ACTIVE
AppliesTo: v1
Type: Runtime Specification

## Purpose

`UITweenSequence`는 여러 tween을 조합하는 최소 sequence 계층이다.

## Target Code Path

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Tween/UITweenSequence.cs
```

## Supported Operations

- `Append` : 순차 실행
- `Join` : 동시 실행
- 입력은 `UITransitionPreset` 또는 `UITransitionPresetAsset`

## Execution Semantics

- 같은 append 그룹 안의 join tween은 동시에 실행한다
- 현재 그룹의 tween이 모두 끝나면 다음 append 그룹으로 이동한다
- sequence 전체 cancel 시 남은 tween은 실행되지 않는다
- group duration은 각 preset의 `Delay + Duration` 최대값으로 계산한다
- 각 preset의 `From*` 값은 group 시작 시 즉시 적용된다

## Builder Shape

```csharp
public sealed class UITweenSequence
{
    public bool IsEmpty { get; }
    public UITweenSequence Append(UITransitionPreset preset);
    public UITweenSequence Append(UITransitionPresetAsset asset);
    public UITweenSequence Join(UITransitionPreset preset);
    public UITweenSequence Join(UITransitionPresetAsset asset);
}
```

## Non-Goals

- loop
- nested sequence DSL
- arbitrary branching
- timeline editor
