# AnimSequencePlayer (Animator 기반)

## 0. 목적
여러 AnimationClip을 순서대로 재생하고, 각 스텝별 반복/속도/페이드를 지원하는
Animator 기반 시퀀스 플레이어를 제공한다.

AnimationEvent에 의존하지 않는다.

---

## 1. 포함 범위
- AnimSequenceData (Serializable 데이터)
- AnimSequencePlayer (MonoBehaviour 재생기)
- AnimClipSelectorWindow (Editor - clip 선택 창)
- AnimSequenceStep_Drawer (Editor - 인스펙터 UI)
- Step 단위: clip, speed, repeat, fadeTime

---

## 2. 동작 규약 (Hard)

### Animator 기반
- AnimSequencePlayer는 AnimatorController 기반이며, state 재생은 Animator.Play/CrossFade로 수행한다.
- Playables(PlayableGraph/Mixer) 관련 코드를 사용하지 않는다.

### stateName 규약
- **기본 규약: stateName == step.Clip.name**
- AnimatorController의 state 이름이 clip.name과 일치해야 동작한다.
- 이 규약이 깨지면 동작 보장하지 않는다.

### 완료 판정
- AnimationEvent 사용 금지
- 완료 판정은 AnimatorStateInfo.normalizedTime 기반(루프 count = floor(normalizedTime))

### 데이터 구조
- AnimSequenceData는 [Serializable] 데이터이며 ScriptableObject를 사용하지 않는다.
- AnimSequenceData는 다른 컴포넌트/데이터에 serialized field로 포함하는 사용을 정본으로 한다.
- repeat == Loop 인 스텝은 자동으로 다음 스텝으로 진행하지 않는다.
- 시퀀스 종료 시:
  - OnComplete event + optional callback이 정확히 1회 호출된다.

### Repeat>1 requires looping clip (경고)
- AnimSequenceStep.Repeat가 One이 아닌 경우(예: Two..Ten, Loop) 해당 AnimationClip은 Looping(Loop Time) 이어야 한다.
- Loop가 아닌 clip에 반복을 적용하면 normalizedTime이 증가하지 않아 시퀀스 진행이 block될 수 있다.
- AnimSequencePlayer는 재생 시작 시 반복 스텝을 검사하여, loop가 아닌 clip에 반복이 걸린 경우 Warning 로그를 출력한다.
- 경고만 출력하며 실행은 계속된다(Warn-only, 동작 차단 없음).

### PlayNext 규약
- Loop 스텝에서 다음 스텝으로 진행하려면 AnimSequencePlayer.PlayNext()를 호출한다.
- PlayNext()는 현재 스텝이 Loop가 아니더라도 "강제로 다음 스텝"으로 이동한다.
- 다음 스텝이 없으면 시퀀스를 Complete 처리한다(콜백/OnComplete 1회).

---

## 3. Pause/Stop 규약 (Hard)
- Pause(true)는 Animator.speed를 0으로 설정한다.
- Pause(false)는 현재 스텝 속도로 복구한다.
- Stop(invokeCallback=true)로 종료하면:
  - callback이 있으면 1회 호출 후 내부에서 null 처리한다.
- Stop 시 Animator.speed는 1로 복귀한다.

---

## 4. 페이드 규약 (Hard)
- fadeTime <= 0 이면 Animator.Play로 즉시 전환한다.
- fadeTime > 0 이면 Animator.CrossFade로 전환한다.

---

## 5. Editor 선택 UI 규약 (Hard)
- Inspector에서 AnimatorController의 animationClips 목록으로 clip을 선택 가능해야 한다.
- Selector는 EditorWindow 기반, 클릭 즉시 적용 후 닫힘(Apply 버튼 없음)
- ScriptableWizard 사용 금지

---

## 6. 금지 행동 (Hard)
- Playables 관련 using/graph 코드 잔존 금지
- ScriptableWizard 사용 금지
- AnimationClip에 AddEvent/RemoveEvent 등 에셋 변경 금지

---

## 7. 파일 구조

```
Runtime/Unity/Animation/
  AnimSequence.cs         # Serializable 데이터 (AnimSequenceData, AnimSequenceStep, AnimPlayCount)
  AnimSequencePlayer.cs   # MonoBehaviour 재생기 (Animator 기반)

Editor/Animation/
  AnimClipSelectorWindow.cs   # clip 선택 EditorWindow
  AnimSequenceStep_Drawer.cs  # AnimSequenceStep PropertyDrawer
```

---

## 8. API 요약

### AnimPlayCount (enum)
```csharp
public enum AnimPlayCount
{
    Loop = 0,
    One = 1,
    Two = 2,
    // ... up to Ten = 10
}
```

### AnimSequenceStep (class)
```csharp
[Serializable]
public sealed class AnimSequenceStep
{
    // 규약: Animator stateName == Clip.name
    public AnimationClip Clip;
    public float Speed = 1f;
    public AnimPlayCount Repeat = AnimPlayCount.One;
    public float FadeTime = 0f;
}
```

### AnimSequenceData (Serializable)
```csharp
[Serializable]
public sealed class AnimSequenceData
{
    public AnimSequenceStep[] Steps;
    public bool IsValid();
}
```

### AnimSequencePlayer (MonoBehaviour)
```csharp
[RequireComponent(typeof(Animator))]
public sealed class AnimSequencePlayer : MonoBehaviour
{
    public event Action OnComplete;
    public bool IsPlaying { get; }
    public bool IsPaused { get; }
    public float PlaySpeed { get; set; }

    // Inspector에서 기본 시퀀스 할당 가능
    [SerializeField] private AnimSequenceData _defaultSequence;

    public void PlayDefault(float playSpeed = 1f, Action onComplete = null, int startIndex = 0);
    public void Play(AnimSequenceData sequence, float playSpeed = 1f, Action onComplete = null, int startIndex = 0);
    public void Stop(bool invokeCallback);
    public void Pause(bool paused);

    // Loop 스텝 block 상태에서 다음 스텝으로 진행 (다음이 없으면 Complete)
    public bool PlayNext();
}
```

### AnimClipSelectorWindow (Editor)
```csharp
public sealed class AnimClipSelectorWindow : EditorWindow
{
    public static AnimClipSelectorWindow Open(SerializedProperty targetProperty, RuntimeAnimatorController controller);
}
```

### AnimSequenceStep_Drawer (Editor)
```csharp
[CustomPropertyDrawer(typeof(AnimSequenceStep))]
public sealed class AnimSequenceStep_Drawer : PropertyDrawer
{
    // Clip ObjectField + Select 버튼
    // Speed, Repeat, FadeTime 필드
}
```
