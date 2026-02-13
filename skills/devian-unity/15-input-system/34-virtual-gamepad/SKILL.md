# 34-virtual-gamepad

Status: ACTIVE
AppliesTo: v11
Type: Component Specification

## 목적

`VirtualGamepad`는 **InputSystem 커스텀 가상 디바이스**로, UI(UIVirtualMovePad / UIVirtualLookPad 등)의 입력을 InputAction 바인딩 경로(`<VirtualGamepad>/move` 등)로 주입한다.

- InputManager는 VirtualGamepad 존재를 모른다 (수정 없음)
- VirtualGamepadDriver가 `QueueStateEvent`로 상태 주입
- CompoSingleton 기반 — 씬에서만 활성, 전역 접근 가능

---

## 범위

### 포함

- `VirtualGamepadState` — 상태 구조체 (move, look, buttons)
- `VirtualGamepad` — InputSystem 커스텀 InputDevice
- `VirtualGamepadUtility` — 레이아웃 등록 / 디바이스 생성·제거 헬퍼
- `VirtualGamepadDriver` — CompoSingleton, SetMove/SetLook/SetButton API

### 제외

- UI 컴포넌트 (UIVirtualMovePad / UIVirtualLookPad → `com.devian.samples`, VirtualGamepadDriver 연동 내장)
- 바인딩 설치 (→ `32-input-manager` InputManagerInspector)
- InputManager 수정

---

## 네임스페이스

```csharp
namespace Devian
```

---

## 핵심 규약 (Hard Rule)

### 1. 바인딩 경로

| 컨트롤 | 경로 | 타입 |
|--------|------|------|
| 이동 | `<VirtualGamepad>/move` | Stick |
| 시점 | `<VirtualGamepad>/look` | Stick |
| 대시 | `<VirtualGamepad>/dash` | Button |

### 2. VirtualGamepadDriver 수명

- `CompoSingleton<VirtualGamepadDriver>` 상속
- `DontDestroy => false` — 씬 전환 시 파괴
- `Awake()`: `base.Awake()` 호출 후 디바이스 생성
- `OnDestroy()`: 디바이스 제거 후 `base.OnDestroy()` 호출

### 3. 상태 주입 타이밍

- `SetMove/SetLook/SetButton` 호출 시 dirty 플래그 세팅
- `Update()`에서 dirty일 때만 `InputSystem.QueueStateEvent` 1회 호출

### 4. InputManager 독립

InputManager는 VirtualGamepad/Driver를 참조하지 않는다. InputAction 바인딩을 통해서만 간접 연결된다.

### 5. 바인딩 설치는 InputManager에서

VirtualGamepad 바인딩 설치는 `InputManagerInspector`의 "Install/Ensure VirtualGamepad Bindings" 버튼으로 수행한다. VirtualGamepadDriver에는 설치 관련 필드/인스펙터가 없다.

### 6. UI 컴포넌트 연결

- `UIVirtualMovePad`는 매 프레임 `VirtualGamepadDriver.SetMove(CurrentValue)`만 호출한다.
- `UIVirtualLookPad`는 매 프레임 `VirtualGamepadDriver.SetLook(CurrentValue)`만 호출한다.
- 각 패드는 전용 축만 주입하며 분기 로직 없음.

---

## API 시그니처

```csharp
// --- VirtualGamepadState ---
public struct VirtualGamepadState : IInputStateTypeInfo
{
    public Vector2 move;
    public Vector2 look;
    public uint buttons;
    public static VirtualGamepadState Create(Vector2 move, Vector2 look, uint buttons = 0);
}

// --- VirtualGamepad ---
[InputControlLayout(stateType = typeof(VirtualGamepadState))]
public class VirtualGamepad : InputDevice
{
    public StickControl move { get; }
    public StickControl look { get; }
    public ButtonControl dash { get; }
}

// --- VirtualGamepadUtility ---
public static class VirtualGamepadUtility
{
    public static void EnsureLayout();
    public static VirtualGamepad CreateDevice();
    public static void RemoveDevice(VirtualGamepad device);
    public static VirtualGamepad GetDevice();
}

// --- VirtualGamepadDriver ---
public sealed class VirtualGamepadDriver : CompoSingleton<VirtualGamepadDriver>
{
    protected override bool DontDestroy => false;
    public void SetMove(Vector2 value);
    public void SetLook(Vector2 value);
    public void SetButton(uint bits);
}
```

---

## 파일 경로

| 타입 | 경로 |
|------|------|
| VirtualGamepadState | `com.devian.foundation/Runtime/Unity/Input/VirtualGamepad/VirtualGamepadState.cs` |
| VirtualGamepad | `com.devian.foundation/Runtime/Unity/Input/VirtualGamepad/VirtualGamepad.cs` |
| VirtualGamepadUtility | `com.devian.foundation/Runtime/Unity/Input/VirtualGamepad/VirtualGamepadUtility.cs` |
| VirtualGamepadDriver | `com.devian.foundation/Runtime/Unity/Input/VirtualGamepad/VirtualGamepadDriver.cs` |

---

## DoD (Definition of Done)

- [ ] 4개 Runtime 파일이 `Runtime/Unity/Input/VirtualGamepad/` 에 위치
- [ ] 모든 파일이 `namespace Devian` 사용
- [ ] VirtualGamepadDriver: CompoSingleton + DontDestroy=false + base.Awake()
- [ ] `Samples~/VirtualGamepad` 완전 삭제
- [ ] InputManager 코드 변경 없음
- [ ] UPM ↔ UnityExample 동일

---

## Reference

- 인덱스: `10-base-system/SKILL.md`
- 입력 관리: `32-input-manager/SKILL.md`
- 입력 소비: `33-input-controller/SKILL.md`
- UI 샘플: `com.devian.samples/Samples~/VirtualPad/`
