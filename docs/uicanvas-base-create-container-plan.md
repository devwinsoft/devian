# UICanvas Base + CreateContainer 계획

---

## 1. 목적

다음 구조 변경을 한 번에 정리한다.

1. 비제네릭 `UICanvas` 추상 베이스 도입
2. `UICanvas<TCanvas> : UICanvas` 구조로 변경
3. `UIPanel`이 owner canvas와 `Canvas` 컴포넌트에 직접 접근 가능하도록 정리
4. `UIPanel.CreateContainer<T>()` 추가
5. 이미 제거된 `UICanvas.CreatePanel<>()`는 재도입하지 않음

핵심 목표는 `UIPanel`이 `ownerBase as UICanvas`를 통해 공통 정보를 얻고,
동적 container subtree를 현재 canvas lifecycle에 안전하게 편입시키는 것이다.

---

## 2. 기준 소스

현재 최신 기준은 `Assets/Samples` 쪽이다.

주요 파일:

- `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/UIPackage/Runtime/UICanvas.cs`
- `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/UIPackage/Runtime/UIPanel.cs`
- `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/UIPackage/Runtime/Container/UIBaseContainer.cs`
- `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/UIPackage/Runtime/Container/UIScrollContainer.cs`

구현은 이 경로를 먼저 수정하고, 필요하면 mirror 경로로 sync한다.

---

## 3. 현재 문제

### 3.1 UIPanel은 Canvas를 직접 모른다

현재 `UIPanel`은 `ownerBase : MonoBehaviour`만 가진다.
이 구조에서는 panel이 동적 container를 만들 때 필요한 `Canvas`를 직접 얻기 어렵다.

### 3.2 동적 container는 `_Init` / `_InitComplete` 둘 다 필요하다

`UIBaseContainer`는 다음 2단계 수명주기를 가진다.

1. `_Init(Canvas canvas)`
2. `_InitComplete()`

특히 `UIScrollContainer`는 `onInitComplete()`에서 frame 수집, section 수집,
logical row build, frame `_InitComplete()`까지 수행한다.
즉 `_Init()`만 호출하면 불완전 초기화다.

### 3.3 호출 시점이 2가지다

`UIPanel.CreateContainer<T>()`는 최소 두 시점을 지원해야 한다.

1. `panel.onInit()` 내부
2. `canvas.Init()` 완료 후 일반 런타임 시점

첫 번째 경우에는 `_InitComplete()`를 즉시 호출하면 순서가 깨질 수 있다.
두 번째 경우에는 `_Init()`와 `_InitComplete()`를 즉시 호출해도 된다.

---

## 4. 결정 사항

### 4.1 비제네릭 UICanvas 도입

목표 구조:

```csharp
public abstract class UICanvas : MonoBehaviour
{
    public Canvas canvas { get; }
}

public abstract class UICanvas<TCanvas> : UICanvas
    where TCanvas : UICanvas
{
    public static TCanvas Instance { get; }
}
```

이렇게 하면 `UIPanel`은 `ownerBase as UICanvas`로 공통 canvas 정보를 바로 얻을 수 있다.

### 4.2 CreatePanel은 유지하지 않는다

`UICanvas.CreatePanel<>()`는 이미 제거된 상태를 유지한다.
이번 작업에서 다시 추가하지 않는다.

### 4.3 UIPanel.CreateContainer<T>()는 subtree 단위로 동작한다

root container 하나만 초기화하는 것이 아니라,
생성된 root 이하의 모든 `UIBaseContainer` subtree를 현재 lifecycle에 편입한다.

이유:

- prefab root 아래에 nested container가 있을 수 있다
- `UIScrollContainer` 같은 타입은 자기 내부 frame를 `onInitComplete()`에서 초기화한다

### 4.4 owner canvas가 lifecycle ownership을 가진다

`UIPanel`은 직접 `_Init/_InitComplete` 순서를 계산하지 않는다.
`UIPanel.CreateContainer<T>()`는 spawn 후 owner canvas helper에 위임한다.

---

## 5. 목표 API

### 5.1 UICanvas

```csharp
public abstract class UICanvas : MonoBehaviour
{
    public Canvas canvas { get; private set; }
    public bool isInitialized { get; private set; }
    public bool isInitComplete { get; private set; }

    public void Init();
    public virtual bool Validate(out string reason);

    internal void RegisterDynamicContainerTree(UIBaseContainer root);
}
```

### 5.2 UICanvas<TCanvas>

```csharp
public abstract class UICanvas<TCanvas> : UICanvas
    where TCanvas : UICanvas
{
    public static TCanvas Instance { get; private set; }
}
```

### 5.3 UIPanel

```csharp
public abstract class UIPanel : MonoBehaviour
{
    protected MonoBehaviour ownerBase { get; private set; }
    protected UICanvas ownerCanvas => ownerBase as UICanvas;

    public T CreateContainer<T>(string prefabName, Transform parent = null)
        where T : UIBaseContainer, IPoolable;
}
```

### 5.4 UIPanel<TCanvas>

```csharp
public abstract class UIPanel<TCanvas> : UIPanel
    where TCanvas : UICanvas
{
    public TCanvas owner { get; private set; }
}
```

---

## 6. Lifecycle 목표

### 6.1 정적 Init 순서

기존 순서는 유지한다.

```text
UICanvas.Init()
  Phase 1: Container._Init(canvas)
  Phase 2: UIPanel._InitFromCanvas(owner)
  Phase 3: Canvas.onInit()
  Phase 4: Container._InitComplete()
  Phase 5: UIPanel._InitComplete()
  Phase 6: Canvas.onInitComplete()
  Phase 7: UI_MESSAGE.InitOnce
```

### 6.2 동적 CreateContainer<T>() 순서

#### Case A: panel.onInit() 내부에서 호출

`canvas.Init()`가 아직 진행 중이다.

필요 동작:

1. prefab spawn
2. root 이하 `UIBaseContainer` subtree 수집
3. 즉시 `_Init(canvas)` 호출
4. `_InitComplete()`는 나중에 canvas phase 순서에 맞춰 호출

#### Case B: canvas.Init() 완료 후 호출

필요 동작:

1. prefab spawn
2. root 이하 `UIBaseContainer` subtree 수집
3. 즉시 `_Init(canvas)` 호출
4. 즉시 `_InitComplete()` 호출

---

## 7. 구현 전략

### 7.1 UICanvas를 공통 owner로 분리

현재 `UICanvas<TCanvas> : MonoBehaviour`의 공통 구현을
비제네릭 `UICanvas : MonoBehaviour`로 올린다.

비제네릭 베이스 책임:

- `Canvas` 캐시
- `Init()` 전체 흐름
- `Validate()`
- init phase/state 추적
- 동적 container subtree 등록 helper

제네릭 파생 책임:

- `static Instance`
- 타입 안전 singleton 정리

### 7.2 init phase/state 추가

권장 enum:

```csharp
internal enum UICanvasInitPhase
{
    None,
    ContainerInit,
    PanelInit,
    CanvasInit,
    ContainerInitComplete,
    PanelInitComplete,
    CanvasInitComplete,
    Completed
}
```

최소 필요 상태:

- `isInitialized`
- `isInitComplete`
- `currentPhase`
- `List<UIBaseContainer> _pendingDynamicContainers`

### 7.3 dynamic container helper 추가

`UICanvas` 내부 helper 역할:

1. root 이하 `UIBaseContainer` 수집
2. 아직 초기화되지 않은 container에 `_Init(canvas)` 호출
3. 현재 phase에 따라:
   - init 중이면 pending queue에 등록
   - init complete 후면 `_InitComplete()` 즉시 호출

권장 시그니처:

```csharp
internal void RegisterDynamicContainerTree(UIBaseContainer root)
```

### 7.4 phase 4에서 pending queue flush

`canvas.Init()` 진행 중 동적으로 추가된 container는
기존 `mContainers` foreach에 자동 포함되지 않을 수 있다.

따라서 phase 4에서 기존 수집분 처리 후,
pending dynamic container queue를 flush해야 한다.

주의:

- 중복 `_InitComplete()` 방지 필요
- 순서는 "Container init complete before Panel init complete" 유지

### 7.5 UIPanel에 ownerCanvas 추가

```csharp
protected UICanvas ownerCanvas => ownerBase as UICanvas;
```

그리고 `UIPanel<TCanvas>` 제약을 `where TCanvas : UICanvas`로 바꾼다.

### 7.6 UIPanel.CreateContainer<T>() 구현

권장 동작:

```csharp
public T CreateContainer<T>(string prefabName, Transform parent = null)
    where T : UIBaseContainer, IPoolable
```

동작:

1. `BundlePool.Spawn<T>(prefabName, parent: parent ?? transform)`
2. `ownerCanvas` null 체크
3. panel이 아직 owner를 받지 못한 상태면 오류 처리
4. spawn된 root를 `ownerCanvas.RegisterDynamicContainerTree(root)`에 위임
5. root 반환

권장 정책:

- `ownerCanvas == null`이면 `InvalidOperationException`
- panel 미초기화 상태에서 호출 금지

---

## 8. 영향 파일

### Runtime

- `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/UIPackage/Runtime/UICanvas.cs`
- `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/UIPackage/Runtime/UIPanel.cs`
- `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/UIPackage/Runtime/Container/UIBaseContainer.cs`

필요 시 확인:

- `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/UIPackage/Runtime/Container/UIScrollContainer.cs`
- `framework-cs/apps/UnityExample/Assets/Scripts/UI/Game/UIGameBagPanel.cs`

### Docs / Skills

- `skills/devian-unity/23-ui-package/11-ui-canvas-system/SKILL.md`
- 필요 시 `skills/devian-unity/23-ui-package/SKILL.md`

### Mirror sync

필요하면 아래 경로 동기화:

- `framework-cs/upm/com.devian.foundation/Samples~/UIPackage/`
- `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/UIPackage/`

단, 현재 최신 구조가 `Assets/Samples` 쪽인 점을 먼저 확인할 것.

---

## 9. 구현 순서

1. `UICanvas` 비제네릭 베이스 도입
2. `UICanvas<TCanvas> : UICanvas`로 변경
3. `UICanvas<TCanvas>` 제약을 `where TCanvas : UICanvas`로 변경
4. `UICanvas`에 init phase/state 추가
5. `UICanvas`에 `RegisterDynamicContainerTree(UIBaseContainer root)` 구현
6. `UIPanel`에 `ownerCanvas` accessor 추가
7. `UIPanel<TCanvas>` 제약을 `where TCanvas : UICanvas`로 변경
8. `UIPanel.CreateContainer<T>()` 구현
9. phase 4 pending queue flush 구현
10. `CreatePanel`이 다시 들어오지 않았는지 확인
11. 스킬 문서 갱신
12. mirror sync 여부 결정 후 반영

---

## 10. 검증 항목

### 정적 초기화

- 기존 `SceneLobby`, `SceneGame`, `SceneLogin`의 canvas init 흐름이 유지된다
- 기존 panel owner cast가 깨지지 않는다

### 동적 container 생성

- `panel.onInit()` 안에서 `CreateContainer<UIScrollContainer>()` 호출 가능
- `canvas.Init()` 완료 후 `CreateContainer<UIScrollContainer>()` 호출 가능
- 두 경우 모두 `UIScrollContainer.IsInitialized == true`
- 두 경우 모두 `UIScrollContainer.onInitComplete()` 기반 frame init이 정상 동작

### 중복 호출 방지

- `_Init()` 중복 호출 없음
- `_InitComplete()` 중복 호출 없음
- pending queue flush 후 stale 참조 없음

### 정적 분석

- `rg`로 `CreatePanel<` 잔재 0건
- `git diff --check` 통과

---

## 11. 오픈 포인트

### 11.1 isInitComplete 이름

`isInitialized`와 `isInitComplete`를 둘 다 둘지,
혹은 `currentPhase`만 public/private 조합으로 관리할지는 구현자가 정하면 된다.

단, `CreateContainer<T>()` 분기에는 "init 완료 여부"를 판별할 수 있어야 한다.

### 11.2 helper 공개 범위

`RegisterDynamicContainerTree`는 `internal` 권장.
`UIPanel` 외 외부 API로 노출할 이유가 현재는 없다.

### 11.3 panel subtree 내부 nested panel

이번 범위는 dynamic container subtree 편입이다.
dynamic panel subtree 편입은 `CreatePanel` 제거로 제외한다.

즉, 새 helper는 `UIBaseContainer`만 대상으로 설계해도 된다.

