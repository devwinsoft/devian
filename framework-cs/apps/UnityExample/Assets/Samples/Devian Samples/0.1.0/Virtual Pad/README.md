# UI Sample

UGUI 기반 UI 컴포넌트 샘플.

## 포함 컴포넌트

- **UIVirtualPad** — UGUI virtual pad (joystick). 터치/드래그 입력을 정규화된 Vector2(-1..1)로 출력. VirtualGamepadDriver가 존재하면 매 프레임 SetMove로 입력 주입.

## 의존성

- Unity UGUI (`UnityEngine.UI`)
- Unity EventSystems (`UnityEngine.EventSystems`)
- Devian Foundation (`Devian.Unity`) — VirtualGamepadDriver 사용 시

## 설정

1. Hierarchy에 빈 GameObject 생성 → Image + RectTransform 추가 (Outer)
2. 자식으로 빈 GameObject 생성 → Image + RectTransform 추가 (Inner/Knob)
3. Outer에 `UIVirtualPad` 컴포넌트 부착
4. `mOuter` = Outer RectTransform, `mInner` = Inner RectTransform

## VirtualGamepad로 입력 완성

UIVirtualPad의 터치 입력을 InputSystem 가상 디바이스(`VirtualGamepad`)로 주입하여,
InputManager가 별도 수정 없이 표준 InputAction으로 입력을 소비하도록 연결한다.

### 설정

1. 씬에 빈 GameObject 생성 → `VirtualGamepadDriver` 컴포넌트 부착
2. UIVirtualPad를 부착하면 VirtualGamepadDriver로 move가 자동 주입된다 (드라이버가 존재할 때)
3. InputActionAsset에서 바인딩 추가:
   - `Player/Move` → `<VirtualGamepad>/move`
   - (옵션) `Player/Look` → `<VirtualGamepad>/look`
4. InputManager는 그대로 InputAction을 읽기만 한다 (수정 없음)
