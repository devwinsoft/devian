# 51-ui-tween-system — Policy

Status: ACTIVE
AppliesTo: v1
Type: Policy / Entry Point

## Purpose

UITweenSystem의 모듈 경계와 구현 규칙을 정의한다.

---

## Hard Rules

- UITween은 UI 전용이다. 지원 대상은 `CanvasGroup`, `RectTransform`, `Transform`만 허용한다.
- UITween은 실행 계층이다. show/hide 타이밍, queue, priority, business rule을 가지면 안 된다.
- 동일 대상의 main transition은 새 실행 시 기존 tween을 cancel 후 교체한다.
- layout / scroll이 소유하는 root `RectTransform`에는 tween을 직접 적용하지 않는다. `LayoutRoot`와 `VisualRoot`를 분리한다.
- panel visibility 전이는 `UIPanel.Show()` / `UIPanel.Hide()`와 `onShow()` / `onHide()` 규약을 따른다.
- `UIPanel.Show()` / `UIPanel.Hide()`는 선택적 자동 훅이다. frame / panel / container / game event handler의 manual `Play(...)`도 1급으로 지원한다.
- tween 데이터는 preset 기반으로 정의한다. transition policy를 코드 분기로 하드코딩하지 않는다.
- preset authoring은 `UITransitionPresetAsset`으로 하고, inspector 선택은 `UI_TRANSITION_PRESET_ID` 경로를 우선한다.
- `Append` / `Join`만 지원한다. loop, nested DSL, 복잡한 builder는 v1 범위에서 제외한다.
- 범용 property tween, reflection setter, gameplay object tween은 금지한다.
- `AnimSequencePlayer`와 `SceneTransManager`는 재사용하지 않는다.
- pooled UI는 `OnDestroy()` cleanup에 의존하지 않는다. 정상 hide / close / despawn 경로에서 명시적으로 cancel한다.
- `UIScrollContainer` virtualized row/item의 hide tween은 v1에서 지원하지 않는다.
- 이 영역 문서에도 `Usage` 섹션은 만들지 않는다.

---

## Runtime Boundary

권장 runtime 경로:

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Tween/
```

권장 구성:

- `UITweenRunner`
- `UITweenHandle`
- `UITweenSequence`
- `UITweenEase`
- `UITransitionPreset`
- `UITransitionPlayer`
