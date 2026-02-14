# 20-samples-virtual-pad

> **패키지:** com.devian.samples
> **샘플명:** Virtual Pad
> **도메인:** devian-upm-samples

---

## 1. 개요

UGUI 기반 Virtual Pad(조이스틱) 컴포넌트 샘플.
터치/드래그 입력을 정규화된 `Vector2(-1..1)`로 출력한다.

---

## 2. 경로

### 2.1 원본 (upm)

```
framework-cs/upm/com.devian.samples/Samples~/VirtualPad/
```

### 2.2 설치 후 위치 (Unity 프로젝트)

```
Assets/Samples/Devian Samples/0.1.0/VirtualPad/
```

---

## 3. 폴더 구조

```
Samples~/VirtualPad/
├── README.md
└── Runtime/
    ├── Devian.Samples.UI.asmdef
    ├── UIVirtualMovePad.cs
    └── UIVirtualLookPad.cs
```

> UIVirtualMovePad는 VirtualGamepadDriver가 존재하면 매 프레임 `SetMove(CurrentValue)`로 입력을 주입한다. 비활성화 시에도 `SetMove(Vector2.zero)`를 주입한다.
>
> UIVirtualLookPad는 VirtualGamepadDriver가 존재하면 매 프레임 `SetLook(CurrentValue)`로 입력을 주입한다. 비활성화 시에도 `SetLook(Vector2.zero)`를 주입한다.
>
> - Look 입력은 기본적으로 미설정일 수 있다. InputManager의 LookKey가 비어있으면 Look은 초기화되지 않은 것으로 간주되며 `onInputLook` 콜백이 발생하지 않는다.
> - Look을 사용하려면 LookKey(예: `Player/Look`)를 명시적으로 설정하고 `<VirtualGamepad>/look` 바인딩을 추가한다.

---

## 4. asmdef 정보

**파일명:** `Devian.Samples.UI.asmdef`

```json
{
  "name": "Devian.Samples.UI",
  "rootNamespace": "Devian",
  "references": ["Devian.UI"]
}
```

`Devian.UI` 참조: `UIPlugInNonDrawing` (RequireComponent).

---

## 5. 의존성

| 패키지 | 필수 | 용도 |
|--------|------|------|
| Unity UGUI | ✅ | `UnityEngine.UI`, `UnityEngine.EventSystems` |
| `com.devian.ui` | ✅ | `UIPlugInNonDrawing` (RequireComponent) |

---

## 6. UIVirtualMovePad / UIVirtualLookPad 컴포넌트

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

---

## 7. 참고

- 정책 문서: `skills/devian-unity/90-samples/01-policy/SKILL.md`
- 샘플 작성 가이드: `skills/devian-unity/90-samples/02-samples-authoring-guide/SKILL.md`
- **이전 위치:** `com.devian.ui/Runtime/Plugins/UIVirtualPad.cs` → `com.devian.samples/Samples~/VirtualPad/Runtime/` 로 이동 후, `UIVirtualMovePad` + `UIVirtualLookPad`로 분리 완료
