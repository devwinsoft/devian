# UI Toast System 계획

## 1. 목표

Devian UI 구조를 유지한 상태에서 overlay 기반의 non-blocking toast system을 추가한다.

구현 범위:

- `UIToastService`
- `UIToastCanvas`
- `UIToastPanel`
- `UIToastGroup`
- `UIToastFrame`
- toast group 설정/queue/duplicate 처리
- `UIToastSettings.asset` 기반 전역 group 설정

기본 규칙:

- toast는 popup처럼 동작하지 않는다
- dim / blocker / interaction을 넣지 않는다
- toast 내부 text는 `string`만 사용한다
- `TEXT_ID`는 toast 밖에서 `string`으로 변환해서 전달한다
- `UIPlugInText`나 별도 text plugin은 사용하지 않는다
- `UIToastFrame`가 `TextMeshProUGUI`를 직접 bind한다

---

## 2. 목표 구조

```text
MobileApplication.onLoadCompletedAsync()
  -> preload UITransitionPresetAsset bundle from ui label
  -> create UIToastCanvas from UI_CANVAS_ID
  -> DontDestroyOnLoad(UIToastCanvas)
  -> UIToastCanvas.Init()

UIToastService
  -> get existing UIToastCanvas
      -> UIToastPanel
          -> GroupRoot(s)
              -> pooled UIToastFrame(s)
```

타입 구조:

- `UIToastService : AutoSingleton<UIToastService>`
- `UIToastSettings : ScriptableObject`
- `UIToastCanvas : UICanvas<UIToastCanvas>, IPoolable`
- `UIToastPanel : UIPanel<UIToastCanvas>`
- `UIToastGroup` (runtime class)
- `UIToastFrame : UIBaseFrame, IPoolable`

---

## 3. 데이터 모델

### 3.1 ToastGroupConfig

```csharp
[Serializable]
public sealed class ToastGroupConfig
{
    public string GroupId;
    public ToastAnchorPreset AnchorPreset;
    public Vector2 AnchoredOffset;
    public int MaxVisibleCount;
    public float DefaultDuration;
    public ToastDuplicatePolicy DuplicatePolicy;
}
```

### 3.2 ToastRequest

```csharp
public readonly struct ToastRequest
{
    public readonly string GroupId;
    public readonly string Message;
    public readonly float? DurationOverride;
    public readonly ToastType ToastType;
}
```

예:

```csharp
UIToastService.Instance.Show(new ToastRequest(
    groupId: "System",
    message: ST_TEXT.Get(textId),
    durationOverride: null,
    toastType: ToastType.Info));
```

### 3.3 Enum

```csharp
public enum ToastType
{
    Info,
    Success,
    Warning,
    Error
}

public enum ToastDuplicatePolicy
{
    Allow,
    IgnoreIfVisible,
    RefreshDurationIfVisible
}

public enum ToastAnchorPreset
{
    TopLeft,
    TopCenter,
    TopRight,
    MiddleLeft,
    MiddleCenter,
    MiddleRight,
    BottomLeft,
    BottomCenter,
    BottomRight
}
```

---

## 4. 각 타입 책임

### 4.1 UIToastService

- 외부 진입점
- 기존 `UIToastCanvas` 조회
- panel 조회
- request 전달
- canvas 생성 책임은 가지지 않는다
- canvas가 아직 준비되지 않았으면 no-op 또는 warning 처리

권장 규칙:

- `UIToastService`는 bootstrap owner가 아니다
- `MobileApplication` load complete 이전에는 toast 사용을 보장하지 않는다

### 4.2 UIToastCanvas

- overlay 전용 canvas owner
- child `UIToastPanel` lifecycle owner
- `MobileApplication`이 생성/보존하는 long-lived canvas
- 생성 직후 `DontDestroyOnLoad(gameObject)` 적용
- root `RectTransform`은 런타임에 full-stretch + `localScale = 1`로 정규화한다

### 4.2.1 MobileApplication 책임

toast canvas bootstrap은 `MobileApplication`이 담당한다.

필드:

```csharp
[SerializeField] private UI_CANVAS_ID _toastCanvasId;
```

load-complete 규칙:

```text
MobileApplication.onLoadCompletedAsync()
  -> if no UIToastCanvas exists:
         BundlePool.Spawn<UIToastCanvas>(_toastCanvasId)
         DontDestroyOnLoad(canvas.gameObject)
         canvas.Init()
```

주의:

- `UIManager.EnsureCanvas<UIToastCanvas>()` 경로는 사용하지 않는다
- canvas는 scene 종속 UI가 아니라 app lifetime overlay로 취급한다

### 4.3 UIToastPanel

- `UIToastService`가 제공하는 전역 `ToastGroupConfig[]` 사용
- `ToastGroupConfig.ToastFrameId`로 group별 frame prefab 선택
- group registry 생성
- group root 생성/배치
- request를 group으로 라우팅
- panel 자체 show/hide tween은 v1 범위가 아니다

### 4.4 UIToastGroup

- group 설정 보유
- active toast 목록 관리
- waiting queue 관리
- duplicate 처리
- visible slot이 비면 다음 toast 표시
- hide tween 완료 시점에 slot release
- group root는 canvas 전체 크기의 stretch rect를 사용한다
- frame 크기는 group이 재정의하지 않고 prefab 기본 `RectTransform` 값을 유지한다

### 4.5 UIToastFrame

- `TextMeshProUGUI` 직접 bind
- style bind
- `UITransitionPlayer`로 show / hide tween 실행
- lifetime timer / hide complete notify
- pooled cleanup (`OnPoolSpawned` / `OnPoolDespawned`)
- bind 직후 `ForceMeshUpdate()`를 호출하고 `_currentMessage`를 별도로 보관한다
- prefab 기본 `RectTransform` 값을 snapshot으로 보관하고, group 배치 시에는 offset만 적용한다

---

## 5. 동작 규약

### 5.1 Show 흐름

```text
Caller
  -> UIToastService.Show(request)
      -> find existing UIToastCanvas
      -> if missing: warn and return
      -> panel.Enqueue(request)
      -> panel routes to group
      -> group shows immediately or queues
```

### 5.2 Frame spawn / bootstrap

`UIToastFrame`는 pooled dynamic frame이므로 group이 직접 bootstrap한다.

```text
group spawn frame
  -> if !frame.isFrameInitialized:
         frame._Init(canvas)
         frame._InitComplete()
  -> frame.Bind(request)
  -> frame.Show(duration, onHidden)
```

규칙:

- frame은 first spawn 시에만 `_Init(canvas)` / `_InitComplete()`를 수행한다
- 재사용 시 초기화는 `OnPoolSpawned()` / `OnPoolDespawned()` cleanup 경로로 처리한다
- tween cancel은 `OnDestroy()`에 의존하지 않는다

### 5.3 Queue 규칙

v1 구현:

- visible slot이 남아 있으면 즉시 표시
- full 상태면 queue에 적재
- active toast의 hide tween이 완료되면 queue에서 다음 toast 표시

slot 규칙:

- lifetime 만료 시 즉시 slot을 비우지 않는다
- hide tween 완료 callback이 온 뒤 slot을 release한다
- 따라서 hide 중인 toast도 `MaxVisibleCount`를 차지한다

### 5.4 Duplicate 규칙

구현 정책:

- `Allow`
  - 동일 message도 새 toast를 생성한다.
  - 새 toast는 base slot에 끼워 넣지 않고, 현재 visible toast들의 실제 높이 누적값 다음 슬롯에 append한다.
- `IgnoreIfVisible`
- `RefreshDurationIfVisible`

duplicate key 기준:

- `Message`
- `ToastType`

visible 판정:

- active list에 있는 toast는 show 중 / visible / hide 중 모두 duplicate 검사 대상이다

### 5.5 Text 규칙

- toast system 내부는 `string`만 받는다
- localization은 외부에서 해소한다
- visible toast는 language 변경 시 자동 재번역하지 않는다

### 5.6 Non-blocking 규칙

- `UIToastCanvas`는 `ScreenSpaceOverlay`
- `GraphicRaycaster` 없음
- toast item graphics는 `raycastTarget = false`
- dim / blocker / button 기본 제공 없음
- `CanvasGroup.blocksRaycasts = false`
- `CanvasGroup.interactable = false`

---

## 6. UI 구성 규약

### 6.1 UIToastCanvas prefab

- `Canvas.renderMode = ScreenSpaceOverlay`
- `GraphicRaycaster` 없음
- child에 `UIToastPanel` 포함

### 6.2 GroupRoot

- panel이 runtime에 group별 `RectTransform` root를 생성
- 위치는 `ToastGroupConfig.AnchorPreset + AnchoredOffset`으로 설정
- stack layout은 group root가 담당

권장 구조:

- `VerticalLayoutGroup` 또는 `HorizontalLayoutGroup`
- `ContentSizeFitter`
- 방향(`Up/Down/Left/Right`)은 layout 종류 + sibling insertion order로 제어

### 6.3 UIToastFrame prefab

- `UIToastFrame` 컴포넌트
- `LayoutRoot`는 layout group의 child로 배치된다
- 내부 `VisualRoot`에 `CanvasGroup` + `UITransitionPlayer`
- `TextMeshProUGUI` 레퍼런스
- type별 style 변경용 graphic 레퍼런스
- pool spawn/despawn 가능 구조

권장 구조:

```text
UIToastFrame (LayoutRoot)
└── VisualRoot
    ├── CanvasGroup
    ├── UITransitionPlayer
    └── Text / Background / Icon
```

규칙:

- layout root 자체는 tween하지 않는다
- `UITransitionPlayer`는 반드시 `VisualRoot`에 부착한다
- show/hide preset은 `UIToastFrame`의 `_showTransitionId` / `_hideTransitionId`로 설정한다

---

## 7. 구현 순서

### Phase 1. 데이터/서비스

- `ToastRequest`
- `ToastGroupConfig`
- enum들
- `UIToastService`
- `UIToastService` missing-canvas fallback 정책

### Phase 2. Canvas/Panel

- `UIToastCanvas`
- `UIToastPanel`
- `MobileApplication`에 `UI_CANVAS_ID _toastCanvasId` 등록
- `MobileApplication.onLoadCompletedAsync()`에서 toast canvas 생성/Init/DontDestroyOnLoad
- `UIToastSettings.asset`에서 group config + toast frame id 조회
- group root 생성/배치

### Phase 3. Group runtime

- `UIToastGroup`
- active list
- queue
- duplicate 처리
- hide-complete 기준 slot release

### Phase 4. Frame/pool

- `UIToastFrame : UIBaseFrame, IPoolable`
- bind/show/hide
- lifetime 완료 notify
- `UIToastFrame` 소유 show/hide transition id와 `UITransitionPlayer.Play(...)` 연동
- `OnPoolSpawned()` / `OnPoolDespawned()` cleanup

### Phase 5. prefab/sample/docs

- toast canvas prefab
- toast frame prefab
- sample group configs
- skill 문서 추가

---

## 8. 생성할 파일

권장 runtime 파일:

- `UIPackage/Runtime/Toast/UIToastService.cs`
- `UIPackage/Runtime/Toast/UIToastCanvas.cs`
- `UIPackage/Runtime/Toast/UIToastPanel.cs`
- `UIPackage/Runtime/Toast/UIToastGroup.cs`
- `UIPackage/Runtime/Toast/UIToastFrame.cs`
- `UIPackage/Runtime/Toast/ToastRequest.cs`
- `UIPackage/Runtime/Toast/ToastGroupConfig.cs`
- `UIPackage/Runtime/Toast/UIToastSettings.cs`
- `UIPackage/Runtime/Toast/ToastEnums.cs`

테스트용 추가 고려:

- `UIToastFrame`에 default show/hide preset asset 연결
- `MobileApplication` inspector에 toast canvas id 연결
- sample code에서 `UIToastService.Show("Tween Test")` 즉시 호출 가능하도록 convenience overload 제공

권장 문서 파일:

- `skills/devian-unity/23-ui-package/42-ui-toast-system/SKILL.md`
- `skills/devian-unity/23-ui-package/SKILL.md` 인덱스 갱신
