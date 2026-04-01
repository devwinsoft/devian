# 12-ui-tween-sequence

Status: ACTIVE
AppliesTo: v2
Type: Runtime Specification

## Purpose

`UITweenSequence`는 여러 transition preset을 단일 timeline으로 조합하는 최소 sequence 계층이다.

## Target Code Path

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Tween/UITweenSequence.cs
```

## Supported Operations

- `Append` : 순차 배치
- `Join` : 현재 append block 시작 시점에 동시 배치
- 입력은 `UITransitionPreset` 또는 `UITransitionPresetAsset`

## Execution Semantics

- 같은 append 그룹 안의 join preset은 같은 sequence offset을 공유한다
- append 그룹이 끝나면 sequence cursor가 다음 그룹으로 이동한다
- group duration은 각 preset 내부 clip들의 `Start_time + Duration` 최댓값이다
- compile 단계에서 각 preset clip의 `Start_time`에 group offset을 더해 단일 timeline으로 flatten한다
- sequence 전체는 runner에서 group loop를 돌지 않고, compiled timeline 1개로 평가된다
- sequence 전체 cancel 시 남은 transition은 실행되지 않는다

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
