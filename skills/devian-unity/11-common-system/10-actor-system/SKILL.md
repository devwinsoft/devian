# 10-actor-system

Status: ACTIVE
AppliesTo: v11
Type: Component Specification

## 목적

`ActorObject` / `ActorController<TOwner>`는 **Actor-Controller 패턴**의 공통 베이스이다.

- `ActorObject` — MonoBehaviour + IPoolable. 컨트롤러 목록 관리, 외부 Init/Clear lifecycle
- `ActorController<TOwner>` — abstract generic MonoBehaviour. Owner에 종속되는 경량 모듈. 자체 등록 없음
- Actor가 `RegisterController<T>()`로 컨트롤러를 등록하고, `Init()`에서 일괄 초기화

---

## 범위

### 포함

- `ActorObject` — Actor lifecycle (Init/Clear), 컨트롤러 등록/해제, 풀 훅
- `ActorController<TOwner>` — Controller lifecycle (Init/Clear), Owner 참조, Priority

### 제외

- 구체적 Actor/Controller 구현 (서브클래스)
- 입력 컨트롤러 (→ `25-input-controller`)
- 풀 매니저 (→ `10-pool-system`)

---

## 네임스페이스

```csharp
namespace Devian
```

---

## 핵심 규약 (Hard Rule)

### 1. Actor Lifecycle

- `Awake()` → `onAwake()` — Unity 콜백. Init 호출 금지
- `Init()` — **외부 호출 전용**. 1회만 실행 (idempotent)
  1. `_initialized = true`
  2. `onInit()` — 서브클래스 확장 훅
  3. 등록된 컨트롤러 순회 → `controller.Init(this)`
  4. `onPostInit()` — 컨트롤러 초기화 완료 후 훅
- `Clear()` — **외부 호출 + OnDestroy/OnPoolDespawned 방어**. 1회만 실행 (idempotent)
  1. `_cleared = true`
  2. `onClear()` — 서브클래스 정리 훅
  3. 컨트롤러 역순 순회 → `controller.Clear()`
  4. `_controllers.Clear()` + `_initialized = false`
  5. `onPostClear()`
- `OnDestroy()` → `Clear()` (방어)

### 2. Controller Lifecycle

- `Awake()` → `onAwake()` — Unity 콜백
- `Init(TOwner owner)` — Actor.Init() 루프에서 호출. 1회만 실행 (idempotent)
  1. `_owner = owner`, `_initialized = true`
  2. `onInit(owner)` — 서브클래스 확장 훅
- `Clear()` — Actor.Clear() 루프에서 호출. virtual. 1회만 실행 (idempotent)
  1. `_cleared = true`
  2. `onClear()` — 서브클래스 정리 훅
  3. `_initialized = false`, `_owner = default`

### 3. RegisterController\<T\>()

- `ActorObject.RegisterController<T>()` — 파라미터 없는 제네릭
  1. `GetComponent<T>()` → null이면 `AddComponent<T>()`
  2. 중복 검사 후 `_controllers`에 추가
  3. Init은 하지 않음 — `Actor.Init()` 루프에서 일괄 초기화
- `ActorObject.RegisterController<T>(GameObject obj)` — 외부 GameObject 대상 제네릭
  1. `obj.GetComponent<T>()` → null이면 `obj.AddComponent<T>()`
  2. 중복 검사 후 `_controllers`에 추가
  3. Init은 하지 않음 — `Actor.Init()` 루프에서 일괄 초기화
- `ActorObject.UnregisterController(ActorController<ActorObject>)` — 목록에서 제거

### 4. Controller 자동 등록 없음

- ActorController에 OnEnable/OnDisable/OnDestroy 없음
- TryRegisterToActor / RegisterSelf / MarkRegistered 없음
- 등록/해제는 오직 Actor의 책임

### 5. Pool 지원

- `IPoolable<ActorObject>` 구현
- `OnPoolSpawned()` — 재사용 준비 (non-virtual)
  1. `_cleared = false`, `_initialized = false`
  2. `onPoolSpawned()` — 서브클래스 확장 훅
- `OnPoolDespawned()` — 정리 (non-virtual)
  1. `onPoolDespawned()` — 서브클래스 정리 훅 (Clear 전)
  2. `Clear()`

### 6. Priority

- `ActorController<TOwner>.Priority` — `virtual int`, 기본값 0
- 서브클래스에서 override 가능 (예: InputController 우선순위)

---

## API 시그니처

```csharp
// --- ActorObject ---
public abstract class ActorObject : MonoBehaviour, IPoolable<ActorObject>
{
    // Lifecycle
    protected virtual void Awake();
    protected virtual void onAwake() { }
    public void Init();
    protected virtual void onInit() { }
    protected virtual void onPostInit() { }
    public virtual void Clear();
    protected virtual void onClear() { }
    protected virtual void onPostClear() { }
    protected virtual void OnDestroy();

    // Pool
    public void OnPoolSpawned();
    protected virtual void onPoolSpawned() { }
    public void OnPoolDespawned();
    protected virtual void onPoolDespawned() { }

    // Controller registry
    public T RegisterController<T>() where T : ActorController<ActorObject>;
    public T RegisterController<T>(GameObject obj) where T : ActorController<ActorObject>;
    public bool UnregisterController(ActorController<ActorObject> controller);
    public IReadOnlyList<ActorController<ActorObject>> Controllers { get; }
    public bool IsInitialized { get; }
    public bool IsCleared { get; }
}

// --- ActorController<TOwner> ---
public abstract class ActorController<TOwner> : MonoBehaviour
{
    public virtual int Priority { get; }     // default 0
    protected virtual void Awake();
    protected virtual void onAwake() { }

    public void Init(TOwner owner);
    protected virtual void onInit(TOwner owner) { }

    public virtual void Clear();
    protected virtual void onClear() { }

    public TOwner Owner { get; }
    public bool IsInitialized { get; }
    public bool IsCleared { get; }
}
```

---

## 파일 경로

| 타입 | 경로 |
|------|------|
| ActorObject | `com.devian.domain.common/Runtime/Unity/Actors/ActorObject.cs` |
| ActorController\<TOwner\> | `com.devian.domain.common/Runtime/Unity/Actors/ActorController.cs` |

---

## DoD (Definition of Done)

- [ ] 모든 파일이 `namespace Devian` 사용
- [ ] ActorObject가 `abstract class`, `IPoolable<ActorObject>` 구현
- [ ] ActorController\<TOwner\>가 `abstract class`, MonoBehaviour 상속
- [ ] Controller 자동 등록 없음 (OnEnable/RegisterSelf 등 없음)
- [ ] `RegisterController<T>()` — 파라미터 없는 제네릭, GetComponent/AddComponent
- [ ] `RegisterController<T>(GameObject obj)` — 외부 GameObject 대상 제네릭
- [ ] Actor.Init()에서 컨트롤러 일괄 Init (등록 시점에 Init 하지 않음)
- [ ] Actor.Clear()에서 컨트롤러 역순 Clear
- [ ] 모든 protected virtual 훅 소문자 시작 (onAwake, onInit, onClear, onPostInit, onPostClear)
- [ ] Controller.Init/Clear idempotent (1회만 실행)
- [ ] Pool 훅: OnPoolSpawned(리셋→onPoolSpawned), OnPoolDespawned(onPoolDespawned→Clear)
- [ ] UPM ↔ UnityExample 동일

---

## Reference

- Parent: `../00-overview/SKILL.md`
- 입력 컨트롤러: `../25-input-controller/SKILL.md`
- 풀 매니저: `../27-pool-system/SKILL.md`
