# UI Sample

UGUI 기반 UI 컴포넌트 샘플.

## 포함 컴포넌트

- **UIVirtualPad** — UGUI virtual pad (joystick). 터치/드래그 입력을 정규화된 Vector2(-1..1)로 출력.

## 의존성

- Unity UGUI (`UnityEngine.UI`)
- Unity EventSystems (`UnityEngine.EventSystems`)

## 설정

1. Hierarchy에 빈 GameObject 생성 → Image + RectTransform 추가 (Outer)
2. 자식으로 빈 GameObject 생성 → Image + RectTransform 추가 (Inner/Knob)
3. Outer에 `UIVirtualPad` 컴포넌트 부착
4. `mOuter` = Outer RectTransform, `mInner` = Inner RectTransform
