# UI Sample

UGUI 기반 UI 컴포넌트 샘플.

## 포함 컴포넌트

- **UIVirtualMovePad** — Move 입력을 정규화된 Vector2(-1..1)로 출력. VirtualGamepadDriver가 존재하면 매 프레임 `SetMove`로 주입.
- **UIVirtualLookPad** — Look 입력을 정규화된 Vector2(-1..1)로 출력. VirtualGamepadDriver가 존재하면 매 프레임 `SetLook`로 주입.

## 의존성

- Unity UGUI (`UnityEngine.UI`)
- Unity EventSystems (`UnityEngine.EventSystems`)
- Devian Foundation (`Devian.Unity`) — VirtualGamepadDriver 사용 시

## 설정

1. Hierarchy에 빈 GameObject 생성 → Image + RectTransform 추가 (Outer)
2. 자식으로 빈 GameObject 생성 → Image + RectTransform 추가 (Inner/Knob)
3. Outer에 `UIVirtualMovePad` (또는 `UIVirtualLookPad`) 컴포넌트 부착
4. `mOuter` = Outer RectTransform, `mInner` = Inner RectTransform

## VirtualGamepad로 입력 완성

UIVirtualMovePad / UIVirtualLookPad의 터치 입력을 InputSystem 가상 디바이스(`VirtualGamepad`)로 주입하여,
InputManager가 별도 수정 없이 표준 InputAction으로 입력을 소비하도록 연결한다.

### 설정

1. 씬에 빈 GameObject 생성 → `VirtualGamepadDriver` 컴포넌트 부착
2. Move용 패드에 `UIVirtualMovePad` 부착 → `<VirtualGamepad>/move`로 주입
3. Look용 패드에 `UIVirtualLookPad` 부착 → `<VirtualGamepad>/look`로 주입
4. InputActionAsset에서 바인딩 추가:
   - `Player/Move` → `<VirtualGamepad>/move`

   - Look을 쓰려면:
     1) InputManager의 `LookKey`(예: `Player/Look`)를 **명시적으로 설정**하고
     2) `Player/Look` → `<VirtualGamepad>/look` 바인딩을 추가한다.

   - LookKey가 비어있으면 Look은 "초기화 안 됨"으로 간주되어 `onInputLook` 콜백이 발생하지 않는다.
5. InputManager는 그대로 InputAction을 읽기만 한다 (수정 없음)
