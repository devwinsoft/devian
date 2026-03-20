# UI Tween System 계획

## 1. 목표

`UIPackage`에 UI 전용 최소 tween / transition 계층을 추가한다.

목적:

- `UIToastFrame`의 show / hide 연출
- 향후 `UIPanel` / `UIPopup`의 show / hide 연출

이 시스템은 범용 tween 엔진이 아니다.

지원 범위:

- `CanvasGroup.alpha`
- `RectTransform.anchoredPosition`
- `Transform.localScale`
- `duration`
- `delay`
- `easing`
- `cancel`
- `onComplete`
- `sequence` (`Append` / `Join`)

비범위:

- reflection 기반 tween
- 범용 property tween
- path animation
- editor tooling
- gameplay object tween

---

## 2. 위치와 스킬 그룹

신규 스킬 그룹:

- `skills/devian-unity/23-ui-package/51-ui-tween-system/SKILL.md`

인덱스 갱신:

- `skills/devian-unity/23-ui-package/SKILL.md`

권장 runtime 경로:

- `UIPackage/Runtime/Tween/`

권장 파일:

- `UITweenRunner.cs`
- `UITweenHandle.cs`
- `UITweenSequence.cs`
- `UITweenEase.cs`
- `UITransitionPreset.cs`
- `UITransitionPresetAsset.cs`
- `UI_TRANSITION_PRESET_ID.cs`
- `UITransitionPlayer.cs`

---

## 3. 핵심 타입

### 3.1 UITweenRunner

역할:

- 실제 tween 실행 엔진
- 시간 기반 보간
- coroutine 기반 실행

권장 구조:

- `AutoSingleton<UITweenRunner>` 또는 동일 역할의 hidden runner
- 모든 tween 실행은 이 runner를 통해서만 수행

주의:

- `AnimSequencePlayer` 재사용하지 않음
- `SceneTransManager` fade 파이프라인과 분리

### 3.2 UITweenHandle

역할:

- 실행 중인 tween 참조
- cancel
- 상태 조회

최소 API:

```csharp
public sealed class UITweenHandle
{
    public bool IsRunning { get; }
    public bool IsCompleted { get; }
    public bool IsCanceled { get; }
    public void Cancel();
}
```

### 3.3 UITweenSequence

역할:

- 여러 tween 조합
- `Append` = 순차 실행
- `Join` = 동시 실행

v1 규칙:

- sequence는 UI transition 조합용 최소 기능만 제공
- 복잡한 DSL은 만들지 않는다

### 3.4 UITransitionPreset

역할:

- UI 연출 데이터 payload

권장 내용:

- `duration`
- `delay`
- `easing`
- alpha 사용 여부 / from / to
- anchoredPosition 사용 여부 / from / to
- scale 사용 여부 / from / to

권장 방향:

- `UITransitionPreset`은 순수 serializable data로 유지한다
- editor authoring과 inspector 선택은 `UITransitionPresetAsset : ScriptableObject`로 한다
- show preset과 hide preset은 별도 asset 또는 별도 preset data로 가진다

### 3.5 UITransitionPresetAsset

역할:

- editor에서 선택 가능한 tween preset asset
- `UITransitionPreset` data를 담는 authoring wrapper

권장 규칙:

- `AssetManager.GetAsset<UITransitionPresetAsset>(id.Value)`로 조회 가능해야 한다
- inspector 선택은 `UI_TRANSITION_PRESET_ID` drawer / selector 경로를 우선 사용한다
- asset이 ID를 상속하지는 않는다. 이 저장소의 기존 패턴대로 `ScriptableObject asset`과 `string wrapper ID`를 분리한다
- direct asset reference는 editor tooling이나 로컬 실험 용도로만 허용하고, 런타임 참조 계약은 ID 우선으로 맞춘다

### 3.6 UITransitionPlayer

역할:

- preset을 실제 UI 대상에 적용
- 동일 대상의 기존 main transition cancel 후 교체

대상:

- `CanvasGroup`
- `RectTransform`
- `Transform`

권장 규칙:

- player는 정책을 가지지 않는다
- `UIPanel.Show()` / `UIPanel.Hide()`는 선택적 integration point일 뿐이다
- concrete panel / container / frame / game event handler가 임의 시점에 manual `Play(...)`를 호출할 수 있어야 한다
- player는 실행만 한다

---

## 4. 권장 API

### 4.1 UITweenEase

v1 easing:

```csharp
public enum UITweenEase
{
    Linear,
    InQuad,
    OutQuad,
    InOutQuad
}
```

### 4.2 UITransitionPreset

```csharp
[Serializable]
public sealed class UITransitionPreset
{
    public float Duration;
    public float Delay;
    public UITweenEase Ease;

    public bool UseAlpha;
    public float FromAlpha;
    public float ToAlpha;

    public bool UseAnchoredPosition;
    public Vector2 FromAnchoredPosition;
    public Vector2 ToAnchoredPosition;

    public bool UseScale;
    public Vector3 FromScale;
    public Vector3 ToScale;
}
```

- `UseAnchoredPosition`은 절대 좌표 지정이 아니라, play group 시작 시점의 현재 `anchoredPosition` 기준 offset으로 동작한다.

### 4.3 UITransitionPlayer

```csharp
public sealed class UITransitionPlayer : MonoBehaviour
{
    public UITweenHandle Play(UITransitionPreset preset, Action onComplete = null);
    public UITweenHandle Play(UITransitionPresetAsset asset, Action onComplete = null);
    public UITweenHandle Play(UI_TRANSITION_PRESET_ID id, Action onComplete = null);
    public void Cancel();
}
```

---

## 5. 실행 규약

### 5.1 한 개 main transition 채널

- `UITransitionPlayer`는 `_mainHandle` 하나만 가진다
- 새 transition이 시작되면 기존 `_mainHandle`은 cancel된다

즉:

- 동일 대상 중복 실행 시 replace
- global priority / queue는 없다

### 5.2 Play 규칙

`Play(preset)` 동작:

1. 기존 main handle cancel
2. preset의 from 값을 즉시 적용
3. delay 대기
4. duration 동안 보간
5. to 값 적용
6. complete callback 호출

### 5.3 Cancel 규칙

- cancel 시 현재 coroutine / 실행 중 sequence 중단
- cancel된 tween의 complete callback은 호출하지 않는다

### 5.4 Sequence 규칙

- `Append`는 다음 그룹을 순차 실행
- `Join`은 같은 그룹에서 동시 실행
- group 내 tween이 모두 끝나면 다음 append 그룹으로 이동

---

## 6. 대상 바인딩 규약

`UITransitionPlayer`는 외부 target 참조를 serialize하지 않는다.

규칙:

- `[RequireComponent(typeof(RectTransform))]`로 같은 GameObject의 `RectTransform`를 필수로 사용한다
- alpha는 같은 GameObject의 `CanvasGroup`에 적용한다
- anchoredPosition은 같은 GameObject의 `RectTransform`에 적용한다
- scale은 같은 GameObject의 `transform.localScale`에 적용한다
- `CanvasGroup`가 없고 preset이 alpha를 요구하면 alpha 채널만 경고 후 skip 한다

권장 기본:

- `UITransitionPlayer`는 tween 대상과 같은 GameObject에 붙인다
- alpha가 필요하면 같은 GameObject에 `CanvasGroup`를 같이 붙인다

### 6.1 LayoutRoot / VisualRoot 분리

layout이나 scroll이 소유하는 root에는 tween을 직접 적용하지 않는다.

권장 구조:

```text
Frame / Panel / Container Root (LayoutRoot)
└── VisualRoot
    └── 실제 그래픽 / 텍스트 / 배경
```

규칙:

- layout / scroll / section 배치가 잡는 위치는 `LayoutRoot`가 소유한다
- `UITransitionPlayer`는 `VisualRoot`의 alpha / anchoredPosition / scale만 변경한다
- root `RectTransform`을 layout system과 tween system이 동시에 소유하면 안 된다

### 6.2 Panel / Container / Frame 부착 규약

- `UIPanel`은 base `Show()` / `Hide()`에서 tween을 호출할 수 있지만, 그것만 유일한 진입점은 아니다
- `UIBaseContainer`에는 붙일 수 있지만, layout owner인 container root 자체를 tween하면 안 된다
- `UIBaseFrame`가 기본 부착 지점이다. 특히 toast / popup body 같은 display unit에 우선 적용한다
- item add, reward gain, badge pulse 같은 game event에서는 concrete frame / panel / container가 manual `Play(...)`를 직접 호출한다

권장 우선순위:

1. `UIBaseFrame`
2. `UIPanel`
3. concrete `UIBaseContainer`

### 6.3 Pool / Destroy 규약

pooled UI는 `OnDestroy()`에 tween cleanup을 의존하면 안 된다.

규칙:

- 정상 hide / close 경로에서 `UITransitionPlayer.Cancel()`을 명시적으로 호출한다
- pooled frame은 `OnPoolDespawned()` 또는 despawn 직전 명시적 경로에서 cancel한다
- shutdown 상태에서는 `onDestroy()`가 호출되지 않을 수 있으므로 destroy hook만으로 cleanup을 보장하지 않는다

### 6.4 Scroll Virtualization 제약

현재 scroll 계층의 virtualized item에는 hide tween을 바로 붙일 수 없다.

이유:

- `UISimpleFrame.UnbindRow()`는 즉시 `SetActive(false)`를 수행한다
- `UIGridFrame.UnbindRow()`는 즉시 `BundlePool.Despawn()`를 수행한다
- `UIScrollContainer`는 visible range에 따라 row enter/exit를 즉시 처리한다

따라서 v1 규칙:

- `UIScrollContainer`의 virtualized row/item에는 hide tween을 지원하지 않는다
- scroll item에 tween이 필요하면 내부 `VisualRoot`의 show tween 정도만 허용한다
- hide tween이 필요하면 future work로 unbind 계약을 "비동기 완료 후 제거"로 바꿔야 한다

---

## 7. 기본 연출 프리셋

### 7.1 Toast Show

- alpha: `0 -> 1`
- position.y: `+10 ~ +20 -> 0`
- duration: `0.2s`
- ease: `OutQuad`

### 7.2 Toast Hide

- alpha: `1 -> 0`
- position.y: `0 -> +10`
- duration: `0.15s`
- ease: `InQuad`

### 7.3 Panel / Popup Show

- alpha: `0 -> 1`
- scale: `0.95 -> 1`
- duration: `0.2s`
- ease: `OutQuad`

### 7.4 Panel / Popup Hide

- alpha: `1 -> 0`
- scale: `1 -> 0.95`
- duration: `0.15s`
- ease: `InQuad`

---

## 8. 역할 분리

- `UIPanel` / `Container` / `Frame` / game event handler -> transition 시작 시점 결정
- `Frame` -> transition 대상 참조 보유
- `UITween` -> 연출 실행

금지:

- `UITween`가 queue 정책을 가지는 것
- `UITween`가 popup/toast 상태를 판단하는 것
- `UITween`가 business logic을 가지는 것

---

## 9. 구현 단계

### Phase 1. 런타임 골격

- `UITweenEase`
- `UITweenHandle`
- `UITweenRunner`

### Phase 2. Sequence

- `UITweenSequence`
- `Append`
- `Join`

### Phase 3. Transition

- `UITransitionPreset`
- `UITransitionPresetAsset`
- `UI_TRANSITION_PRESET_ID`
- `UITransitionPlayer`
- editor selector / drawer 연결

### Phase 4. Toast 연동

- `UIToastFrame` show / hide 연출 연결
- 중복 실행 cancel 확인

### Phase 5. Panel 연동 준비

- `UIPanel.Show()` / `UIPanel.Hide()` override 경로에서 재사용 가능한 show / hide preset 규약 문서화
- panel hide tween은 `onHide()` override가 deactivate 시점을 직접 소유한다는 계약 정리
- manual play 예시를 같이 문서화한다

### Phase 6. 스킬 문서

- `51-ui-tween-system/SKILL.md`
- `23-ui-package/SKILL.md` 인덱스 추가

---

## 10. 완료 기준

1. `UIToastFrame` show / hide 연출이 정상 동작한다
2. `UIPanel.Show()` / `UIPanel.Hide()`에 재사용 가능한 구조가 된다
3. 동일 대상 중복 실행 시 기존 tween이 cancel된다
4. `Append` / `Join` sequence가 동작한다
5. preset asset을 inspector에서 선택 가능하다
6. frame / panel / game event에서 manual `Play(...)`가 가능하다
