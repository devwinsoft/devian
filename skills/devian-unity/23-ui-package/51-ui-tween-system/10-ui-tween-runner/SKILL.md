# 10-ui-tween-runner

Status: ACTIVE
AppliesTo: v1
Type: Runtime Specification

## Purpose

`UITweenRunner`는 UI tween 실행 엔진이다.
시간 기반 보간, delay 대기, easing 계산, completion / cancel 처리의 실제 실행을 담당한다.

## Target Code Path

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Tween/UITweenRunner.cs
```

## Responsibilities

- tween job 실행
- coroutine 기반 update loop
- `Time.unscaledDeltaTime` 기준 진행
- 진행률 계산 (`0 -> 1`)
- easing 적용
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

- single preset은 내부적으로 1-group sequence처럼 실행한다
- join group은 runner가 한 coroutine에서 같이 구동한다
- 각 group 시작 시 `UITransitionPlayer`가 현재 target 상태를 snapshot하고, anchoredPosition 채널은 그 snapshot을 기준 offset으로 적용한다
- runner는 target 참조를 소유하지 않는다. 실제 property 적용은 `UITransitionPlayer`가 한다
