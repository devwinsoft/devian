# 61-virtual-gamepad

Status: ACTIVE
AppliesTo: v11
Type: Component Specification

## SSOT

- `com.devian.samples/Samples~/MobileSystem/Runtime/VirtualGamepad`

## Components

- `UIVirtualMovePad`
- `UIVirtualLookPad`

---

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
- `UIVirtualMovePad` — UGUI 기반 Virtual Pad, Move 입력 주입
- `UIVirtualLookPad` — UGUI 기반 Virtual Pad, Look 입력 주입

### 제외

- 바인딩 설치 (→ `31-input-manager` InputManagerInspector)
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

> UIVirtualMovePad는 VirtualGamepadDriver가 존재하면 매 프레임 `SetMove(CurrentValue)`로 입력을 주입한다. 비활성화 시에도 `SetMove(Vector2.zero)`를 주입한다.
>
> UIVirtualLookPad는 VirtualGamepadDriver가 존재하면 매 프레임 `SetLook(CurrentValue)`로 입력을 주입한다. 비활성화 시에도 `SetLook(Vector2.zero)`를 주입한다.
>
> - Look 입력은 기본적으로 미설정일 수 있다. InputManager의 LookKey가 비어있으면 Look은 초기화되지 않은 것으로 간주되며 `onInputLook` 콜백이 발생하지 않는다.
> - Look을 사용하려면 LookKey(예: `Player/Look`)를 명시적으로 설정하고 `<VirtualGamepad>/look` 바인딩을 추가한다.

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
| VirtualGamepadState | `com.devian.samples/Samples~/MobileSystem/Runtime/VirtualGamepad/VirtualGamepadState.cs` |
| VirtualGamepad | `com.devian.samples/Samples~/MobileSystem/Runtime/VirtualGamepad/VirtualGamepad.cs` |
| VirtualGamepadUtility | `com.devian.samples/Samples~/MobileSystem/Runtime/VirtualGamepad/VirtualGamepadUtility.cs` |
| VirtualGamepadDriver | `com.devian.samples/Samples~/MobileSystem/Runtime/VirtualGamepad/VirtualGamepadDriver.cs` |
| UIVirtualMovePad | `com.devian.samples/Samples~/MobileSystem/Runtime/VirtualGamepad/UIVirtualMovePad.cs` |
| UIVirtualLookPad | `com.devian.samples/Samples~/MobileSystem/Runtime/VirtualGamepad/UIVirtualLookPad.cs` |

---

## UIVirtualMovePad / UIVirtualLookPad 컴포넌트

### Inspector Setup

Hierarchy 예시:
- `VirtualPad (Image + RectTransform)` — Outer
  - `Knob (Image + RectTransform)` — Inner

컴포넌트:
- `UIVirtualMovePad` (또는 `UIVirtualLookPad`)를 Outer 오브젝트에 부착
- `mOuter` = Outer RectTransform
- `mInner` = Inner RectTransform

> UIVirtualMovePad / UIVirtualLookPad requires `CanvasRenderer` and `UIPlugInNonDrawing` via `RequireComponent`.

### Parameters

| 필드 | 타입 | 설명 |
|------|------|------|
| `mDynamicCenter` | bool | true면 터치 위치에 패드 중심이 이동 |
| `mRadius` | float | knob 최대 이동 반경 (px) |
| `mDeadzone` | float (0..1) | deadzone |
| `mHideWhenIdle` | bool | 입력 없을 때 그래픽 숨김 |

### Output

| API | 타입 | 설명 |
|-----|------|------|
| `CurrentValue` | Vector2 | 정규화 벡터 (-1..1) |
| `Direction` | Vector2 | 정규화 방향 벡터 (magnitude=0이면 Vector2.zero) |
| `AngleDeg` | float | 방향 각도. Right(1,0)=0°, Up(0,1)=90° |
| `IsPressed` | bool | 현재 눌림 상태 |
| `OnValueChanged` | UnityEvent\<Vector2\> | 값 변경 이벤트 |
| `OnPressed` | UnityEvent | 눌림 이벤트 |
| `OnReleased` | UnityEvent | 해제 이벤트 |

### UGUI 인터페이스

`IPointerDownHandler`, `IDragHandler`, `IPointerUpHandler` 구현.

### 의존성

| 패키지 | 필수 | 용도 |
|--------|------|------|
| Unity UGUI | ✅ | `UnityEngine.UI`, `UnityEngine.EventSystems` |
| `com.devian.ui` | ✅ | `UIPlugInNonDrawing` (RequireComponent) |

---

## DoD (Definition of Done)

- [ ] 4개 Runtime 파일이 `Samples~/MobileSystem/Runtime/VirtualGamepad/` 에 위치
- [ ] 모든 파일이 `namespace Devian` 사용
- [ ] VirtualGamepadDriver: CompoSingleton + DontDestroy=false + base.Awake()
- [ ] InputManager 코드 변경 없음
- [ ] UPM ↔ UnityExample 동일

---

## Reference

- 인덱스: `../../10-foundation/SKILL.md`
- 입력 관리: `../../10-foundation/31-input-manager/SKILL.md`
- 입력 소비: `../../10-foundation/32-input-controller/SKILL.md`
- 정책 문서: `skills/devian-unity/50-mobile-system/01-policy/SKILL.md`
- 샘플 작성 가이드: `skills/devian-unity/07-samples-creation-guide/SKILL.md`
