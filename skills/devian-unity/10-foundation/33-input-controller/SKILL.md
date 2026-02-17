# 33-input-controller

Status: ACTIVE
AppliesTo: v11
Type: Component Specification

## 목적

`BaseInputController`는 **BaseController 기반 입력 소비** 컨트롤러이다.

- `BaseController`를 상속하여 Actor lifecycle에 통합
- `onInit(actor)`에서 `InputManager.Instance.RegisterController(this)` — 등록
- `Clear()` override에서 `UnregisterController(this)` + `base.Clear()` — 해제
  - `BaseBootstrap.IsShuttingDown` 시 매니저 접근 없이 `base.Clear()` 호출
  - `SingletonRegistry.TryGet<InputManager>` 로 안전한 싱글톤 접근
- `IInputSpace` 전략으로 Move 입력을 월드 공간 벡터로 변환
- `InputEnabled` guard로 입력 수신 on/off
- **변화가 있을 때만** 4개 virtual 콜백 호출 (change-only)

---

## 범위

### 포함

- `IBaseInputController` — 입력 컨트롤러 계약
- `BaseInputController` — BaseController 상속, Actor lifecycle 기반 컨트롤러 등록/해제, change-only 콜백
- `IInputSpace` — Move(Vector2) → World(Vector3) 변환 전략 인터페이스
- `WorldXZSpace` — `(x, y) → (x, 0, y)` 탑다운/2D용
- `ViewFlattenedSpace` — 카메라 forward/right y=0 평탄화 후 합성

### 제외

- 입력 수집/정규화/발행 (→ `32-input-manager`)
- 구체적 게임 입력 로직 (서브클래스 구현)

---

## 네임스페이스

```csharp
namespace Devian
```

---

## 핵심 규약 (Hard Rule)

### 1. 컨트롤러 등록 lifecycle (Actor 기반)

- `BaseController`를 상속 → Actor의 Init/Clear lifecycle에 통합
- `onInit(BaseActor actor)`: `InputManager.Instance.RegisterController(this)` + prev 상태 리셋
- `Clear()` override: 종료 방어 + `UnregisterController(this)` + prev 리셋 + `base.Clear()`
  - `BaseBootstrap.IsShuttingDown` 시 매니저 접근 없이 `base.Clear()` 호출
  - `IsCleared` 체크 + `SingletonRegistry.TryGet<InputManager>` 로 안전한 해제
- Actor가 `RegisterController<T>()`로 등록, `Init()` 루프에서 초기화 (자동 등록 없음)
- Priority는 BaseController에서 상속 (`virtual int Priority => 0`) — 중복 선언 없음
- InputManager는 싱글톤을 신뢰 (Bootstrap 보장, SerializeField 없음)
- Bus(IInputBus/InputBus)는 삭제됨 — InputManager가 직접 `__Consume(frame)`을 호출

### 2. InputEnabled guard

`InputEnabled == false`이면 `__Consume` 내부에서 모든 콜백을 무시한다.

### 3. Priority

기본값 0. 서브클래스에서 override 가능.
InputManager가 `_controllersDirty`일 때 Priority 내림차순으로 정렬하여 호출.

### 4. IInputSpace

- `InputSpace` 프로퍼티로 전략 주입
- 서브클래스에서 `InputSpace.ResolveMove(move)`로 월드 벡터 획득

### 5. Change-only 콜백

InputFrame을 매 프레임 받지만, 파생 클래스 콜백은 **변화가 있을 때만** 호출한다.

| 콜백 | 호출 조건 |
|------|----------|
| `onInputMove(Vector2 move)` | 축이 유효(non-NaN) + `(cur - prev).sqrMagnitude > epsilon²` |
| `onInputLook(Vector2 look)` | 축이 유효(non-NaN) + `(cur - prev).sqrMagnitude > epsilon²` |
| `onButtonPress(string key, int index)` | bit가 0→1로 전환된 각 버튼마다 1회 |
| `onButtonRelease(string key, int index)` | bit가 1→0으로 전환된 각 버튼마다 1회 |

- `_axisEpsilon` (SerializeField, default 0.001f) — Move/Look 변화 감지 임계값
- 축이 초기화되지 않은 경우(NaN — 키 미설정/Action 없음) 해당 축의 콜백은 발생하지 않는다
- 유효한 축의 첫 프레임(`_prevMoveValid`/`_prevLookValid == false`)은 항상 콜백 호출

### 6. 버튼 이벤트 규약

- down/up mask를 외부로 넘기지 않고, 내부에서 bitwise로 개별 index를 펼침
- key는 `InputManager.Instance.ButtonKeys[index]`에서 가져온 `"Map/Action"` 문자열
- index가 ButtonKeys 범위 밖이면 key는 빈 문자열 (예외 없음)

### 7. `__Consume` 엔트리 포인트

- `public void __Consume(InputFrame frame)` — InputManager가 호출하는 진입점
- 외부 코드에서 직접 호출하지 않는다
- 내부에서 `_onInputFrame(frame)`을 호출하여 change detection + 4개 콜백 dispatch

---

## API 시그니처

```csharp
// --- IBaseInputController ---
public interface IBaseInputController
{
    bool InputEnabled { get; set; }
    int Priority { get; }
    IInputSpace InputSpace { get; set; }
}

// --- BaseInputController ---
public abstract class BaseInputController : BaseController, IBaseInputController
{
    public bool InputEnabled { get; set; }
    public IInputSpace InputSpace { get; set; }

    // Actor lifecycle (BaseController overrides)
    protected override void onInit(BaseActor actor);   // RegisterController + reset prev
    public override void Clear();                       // UnregisterController + reset prev + base.Clear()

    public void __Consume(InputFrame frame);

    protected virtual void onInputMove(Vector2 move) { }
    protected virtual void onInputLook(Vector2 look) { }
    protected virtual void onButtonPress(string key, int index) { }
    protected virtual void onButtonRelease(string key, int index) { }
}

// --- IInputSpace ---
public interface IInputSpace
{
    Vector3 ResolveMove(Vector2 raw);
}

// --- WorldXZSpace ---
public class WorldXZSpace : IInputSpace
{
    public Vector3 ResolveMove(Vector2 raw);  // → (x, 0, y)
}

// --- ViewFlattenedSpace ---
public class ViewFlattenedSpace : IInputSpace
{
    public ViewFlattenedSpace(Transform cameraTransform);
    public Vector3 ResolveMove(Vector2 raw);  // camera forward/right 평탄화
}
```

---

## 파일 경로

| 타입 | 경로 |
|------|------|
| IBaseInputController | `com.devian.foundation/Runtime/Unity/Input/IBaseInputController.cs` |
| BaseInputController | `com.devian.foundation/Runtime/Unity/Input/BaseInputController.cs` |
| IInputSpace | `com.devian.foundation/Runtime/Unity/Input/IInputSpace.cs` |
| WorldXZSpace | `com.devian.foundation/Runtime/Unity/Input/InputSpaces/WorldXZSpace.cs` |
| ViewFlattenedSpace | `com.devian.foundation/Runtime/Unity/Input/InputSpaces/ViewFlattenedSpace.cs` |

---

## DoD (Definition of Done)

- [ ] 모든 파일이 `namespace Devian` 사용
- [ ] BaseInputController가 BaseController를 상속 (abstract class)
- [ ] 자동 등록 없음 — Actor가 `RegisterController<T>()`로 등록
- [ ] `onInit`에서 RegisterController (Actor lifecycle)
- [ ] `Clear()` override에서 UnregisterController + base.Clear()
- [ ] `Clear()` 종료 방어: `BaseBootstrap.IsShuttingDown` + `SingletonRegistry.TryGet`
- [ ] Priority는 BaseController에서 상속 (중복 선언 없음)
- [ ] InputManager.Instance(싱글톤)으로만 접근 (SerializeField 없음)
- [ ] InputEnabled guard 적용
- [ ] 4개 virtual 콜백: onInputMove, onInputLook, onButtonPress, onButtonRelease
- [ ] 변화 없으면 콜백 호출되지 않음 (change-only)
- [ ] 버튼 이벤트는 개별 key/index로 펼쳐서 호출 (mask 외부 전달 없음)
- [ ] `__Consume` 엔트리 포인트 존재
- [ ] WorldXZSpace / ViewFlattenedSpace 구현
- [ ] UPM ↔ UnityExample 동일

---

## Reference

- 인덱스: `../SKILL.md`
- 입력 수집: `../32-input-manager/SKILL.md`
