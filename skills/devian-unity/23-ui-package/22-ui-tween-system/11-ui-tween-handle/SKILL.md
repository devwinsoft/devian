# 11-ui-tween-handle

Status: ACTIVE
AppliesTo: v1
Type: Runtime Specification

## Purpose

`UITweenHandle`는 실행 중인 tween의 참조다.
cancel과 상태 조회를 제공한다.

## Target Code Path

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Tween/UITweenHandle.cs
```

## Minimum API

```csharp
public sealed class UITweenHandle
{
    public bool IsRunning { get; }
    public bool IsCompleted { get; }
    public bool IsCanceled { get; }
    public void Cancel();
}
```

## Rules

- `Cancel()`은 idempotent 해야 한다
- cancel된 tween의 complete callback은 호출하지 않는다
- 완료 후 handle은 completed 상태로 고정된다
- handle은 실행 정책을 소유하지 않는다
- invalid preset / missing asset / runner unavailable 같은 실패 경로는 canceled handle로 귀결된다
