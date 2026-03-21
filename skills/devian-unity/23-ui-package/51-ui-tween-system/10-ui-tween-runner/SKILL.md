# 10-ui-tween-runner

Status: ACTIVE
AppliesTo: v2
Type: Runtime Specification

## Purpose

`UITweenRunner`는 compiled UI transition 실행 엔진이다.
시간 기반 평가, frame result 적용, completion / cancel 처리의 실제 실행을 담당한다.

## Target Code Path

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Tween/UITweenRunner.cs
```

## Responsibilities

- tween job 실행
- coroutine 기반 update loop
- `Time.unscaledDeltaTime` 기준 진행
- `UITransitionSnapshot` 캡처 시점 관리
- `UICompiledTransitionData` 평가 루프 실행
- `UITransitionFrameResult` 적용 타이밍 관리
- 완료 / cancel 상태 반영

## Non-Responsibilities

- show/hide 정책
- queue / priority
- panel / popup / toast state 판단
- generic property tween

## Structure

현재 구조:

- `AutoSingleton<UITweenRunner>`
- hidden runner object (`HideInHierarchy`)
- 모든 tween 실행은 runner를 통해 수행
- 외부 public surface는 최소화하고, 실제 사용 진입은 `UITransitionPlayer`가 맡는다

## Runtime Notes

- runner는 preset을 직접 해석하지 않는다. 실행 입력은 `UICompiledTransitionData`다
- play 시작 시 `UITransitionPlayer.CaptureSnapshot()`을 1회 호출한다
- 매 프레임 `compiled.Evaluate(elapsed, snapshot)`으로 result를 계산한다
- 매 프레임 `UITransitionPlayer.Apply(result)`를 1회 호출한다
- duration이 `0 이하`면 즉시 final result를 적용하고 완료한다
- runner는 target 참조를 소유하지 않는다. 실제 property 적용은 `UITransitionPlayer`가 한다
