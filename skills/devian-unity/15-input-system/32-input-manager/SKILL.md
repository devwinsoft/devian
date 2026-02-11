# 32-input-manager

Status: ACTIVE
AppliesTo: v11
Type: Component Specification

## 목적

`InputManager`는 **InputActionAsset 기반 입력 수집/정규화/발행** 시스템이다.

- Unity InputSystem의 InputActionAsset을 직접 참조
- Move/Look은 Vector2로, 버튼은 ulong bitset으로 정규화
- 매 프레임 등록된 BaseInputController를 Priority 순으로 직접 호출
- Game / UI 컨텍스트 전환 지원

---

## 범위

### 포함

- `InputManager` — CompoSingleton, Bootstrap 기본 포함
- `InputFrame` — 한 프레임의 정규화된 입력 스냅샷 (readonly struct)
- `InputContext` — Game / UI 모드 enum
- `IInputManager` — InputManager 계약
- `InputButtonMapBuilder` — key("Map/Action") → button index 맵 빌드 유틸

### 제외

- 오브젝트 부착형 입력 소비 (→ `33-input-controller`)
- InputSpace 전략 (→ `33-input-controller`)
- InputAction 바인딩/리매핑 UI

---

## 네임스페이스

```csharp
namespace Devian
```

---

## 핵심 규약 (Hard Rule)

### 1. Serialized 기본값

| 필드 | 기본값 |
|------|--------|
| `_gameplayMapName` | `"Player"` |
| `_uiMapName` | `"UI"` |
| `_moveKey` | `"Player/Move"` |
| `_lookKey` | `"Player/Look"` |

### 2. ButtonMap key 포맷

버튼 키는 `"Map/Action"` 형식이다. 예: `"Player/Attack"`, `"Player/Jump"`.

### 3. ButtonMap 정렬 및 인덱스

`InputButtonMapBuilder.Build()`는 expectedKeys를 **Ordinal sort** 후 0..63 인덱스를 부여한다.

### 4. 64 버튼 제한

ulong bitset이므로 최대 64개 버튼. 초과 시 `InvalidOperationException`.

`InputFrame.IsDown(int)`은 0..63만 유효하며, 범위 밖은 `false`.

### 5. Context 전환

`SetContext(InputContext)` 호출 시:
- `Game` → Gameplay ActionMap Enable, UI ActionMap Disable
- `UI` → Gameplay ActionMap Disable, UI ActionMap Enable

### 6. Singleton 등록

- `CompoSingleton<InputManager>` — `Awake()`에서 `base.Awake()` 호출 → 타입 등록
- `IInputManager`는 `OnEnable`에서 `Singleton.Register<IInputManager>` (Compo), `OnDisable`에서 `Unregister`
- Bootstrap의 `ensureRequiredComponents()`에서 `ensureComponent<InputManager>()` 호출 → 기본 포함

### 7. Controller Registry Dispatch

`outputEnabled == true`일 때만 매 프레임:
1. Move/Look ReadValue + buttons bitset 합산 → `InputFrame` 생성
2. `_controllersDirty`이면 `_controllers`를 `Priority` 내림차순 정렬
3. 등록된 `BaseInputController.__Consume(frame)`을 순서대로 호출

- `RegisterController(BaseInputController)` / `UnregisterController(BaseInputController)` — 컨트롤러 등록/해제
- Bus(IInputBus/InputBus)는 삭제됨 — 직접 호출 방식으로 대체

---

## API 시그니처

```csharp
// --- InputContext ---
public enum InputContext : byte { Game = 0, UI = 1 }

// --- InputFrame ---
public readonly struct InputFrame
{
    public readonly Vector2 Move;
    public readonly Vector2 Look;
    public readonly ulong ButtonBits;
    public readonly InputContext Context;
    public readonly float Timestamp;

    public InputFrame(Vector2 move, Vector2 look, ulong buttonBits, InputContext context, float timestamp);
    public bool IsDown(int buttonIndex);
}

// --- IInputManager ---
public interface IInputManager
{
    InputActionAsset Asset { get; }
    InputContext Context { get; }
    int GetButtonIndex(string key);
    IReadOnlyList<string> ButtonKeys { get; }
    void SetContext(InputContext context);
    void RebuildButtonMap();
}

// --- InputManager ---
public sealed class InputManager : CompoSingleton<InputManager>, IInputManager
{
    public void RegisterController(BaseInputController controller);
    public void UnregisterController(BaseInputController controller);
}

// --- InputButtonMapBuilder ---
public static class InputButtonMapBuilder
{
    public static Dictionary<string, int> Build(InputActionAsset asset, string[] expectedKeys);
    public static InputAction TryFindActionByKey(InputActionAsset asset, string key);
}
```

---

## Editor 기능

### Refresh Expected Button Keys

`InputManagerInspector` 커스텀 인스펙터에 **"Refresh Expected Button Keys"** 버튼을 제공한다.

**동작:**
1. `InputActionAsset`의 모든 ActionMap을 스캔
2. `action.expectedControlType == "Button"` 인 액션만 수집
3. key 포맷: `"Map/Action"` (예: `"Player/Attack"`)
4. 중복 제거 (`StringComparer.Ordinal`)
5. Ordinal 정렬
6. 64개 초과 시 64개까지만 적용 + 경고 로그
7. `_expectedButtonKeys` 배열을 완전 덮어쓰기
8. `RebuildButtonMap()` 자동 호출 → 내부 버튼 맵 즉시 동기화

**Play Mode:** Inspector Refresh는 Play Mode에서 비활성화된다.

### Install/Ensure VirtualGamepad Bindings

`InputManagerInspector`에 **"Install/Ensure VirtualGamepad Bindings"** 버튼을 제공한다.

**동작:**
1. InputManager의 `_moveKey`, `_lookKey` 값을 읽어 해당 Action을 해석
2. Action에 `<VirtualGamepad>/move`, `<VirtualGamepad>/look` 바인딩이 없으면 추가
3. 이미 존재하면 아무 것도 안 함
4. Undo 지원 + `AssetDatabase.SaveAssets()`

**Play Mode:** Play Mode에서 비활성화된다.

**Editor 파일:** `com.devian.foundation/Editor/Unity/Input/InputManagerInspector.cs`

---

## 파일 경로

| 타입 | 경로 |
|------|------|
| InputContext | `com.devian.foundation/Runtime/Unity/Input/InputContext.cs` |
| InputFrame | `com.devian.foundation/Runtime/Unity/Input/InputFrame.cs` |
| IInputManager | `com.devian.foundation/Runtime/Unity/Input/IInputManager.cs` |
| InputButtonMapBuilder | `com.devian.foundation/Runtime/Unity/Input/InputButtonMapBuilder.cs` |
| InputManager | `com.devian.foundation/Runtime/Unity/Input/InputManager.cs` |
| InputManagerInspector | `com.devian.foundation/Editor/Unity/Input/InputManagerInspector.cs` |

---

## DoD (Definition of Done)

- [ ] 모든 파일이 `namespace Devian` 사용
- [ ] `Devian.Unity.asmdef`에 `Unity.InputSystem` 참조 포함
- [ ] UPM ↔ UnityExample 동일
- [ ] ButtonMap key 64개 이하 검증
- [ ] InputManager 기본값: `"Player"`, `"Player/Move"`, `"Player/Look"`
- [ ] Inspector "Refresh Expected Button Keys" 버튼 동작
- [ ] Inspector "Install/Ensure VirtualGamepad Bindings" 버튼 동작
- [ ] `Devian.Unity.Editor.asmdef`에 `Unity.InputSystem` 참조 포함
- [ ] InputManager: CompoSingleton + Awake base.Awake()
- [ ] BaseBootstrap: ensureRequiredComponents에 InputManager 포함
- [ ] IInputBus / InputBus 파일 삭제됨
- [ ] Update에서 Controller registry dispatch (Priority 순 직접 호출)

---

## Reference

- 인덱스: `10-base-system/SKILL.md`
- 입력 소비: `33-input-controller/SKILL.md`
