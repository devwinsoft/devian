# 10-ui-component-base

Status: ACTIVE
AppliesTo: v11

## Purpose

`UIBaseCanvas` 수명주기에 통합되는 UI 컴포넌트 공통 베이스를 정의한다.
`UIComponentBase`는 `component > frame > container` 순서의 초기화 규약을 제공한다.

## Scope

### Includes
- `UIComponentBase : MonoBehaviour`
- `public void Init(Canvas canvas)` 진입점
- `protected virtual void onInit(Canvas canvas)` 확장점
- `Awake()`에서 `GetComponentInParent<UIBaseCanvas>()` 탐색 후 owner canvas가 이미 초기화된 경우 즉시 `Init(...)`
- `UIBaseCanvas.Init()` / `UIBasePanel.CreateFrame()` / `UIBasePanel.CreateContainer()` 경로와의 연동

### Excludes
- `UIComponentCircleFilter`
- `UIComponentNonDrawing`

두 타입은 기존 부모 타입을 유지한다. 공통 base로 올리지 않는다.

## SSOT

### Code Path

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Component/UIComponentBase.cs
```

### API

```csharp
namespace Devian
{
    public abstract class UIComponentBase : MonoBehaviour
    {
        public bool isInitialized { get; }
        public Canvas canvas { get; }

        public void Init(Canvas canvas);

        protected virtual void onAwake();
        protected virtual void onInit(Canvas canvas);
        protected virtual void onDestroy();
    }
}
```

## Lifecycle

### Init Order

`UIComponentBase`는 `UIBaseCanvas` 초기화에서 가장 먼저 초기화된다.

```text
UIComponentBase.Init(canvas)
-> UIBaseFrame._Init(canvas)
-> UIBaseContainer._Init(canvas)
-> UIBasePanel._InitFromCanvas(owner)
-> UIBaseCanvas.onInit()
```

### Dynamic Trees

- 동적 `CreateFrame()` 경로에서는 frame subtree의 `UIComponentBase`가 frame보다 먼저 초기화된다.
- 동적 `CreateContainer()` 경로에서는 container subtree의 `UIComponentBase`와 frame이 container보다 먼저 초기화된다.
- 정적 `UIBaseCanvas.Init()` 경로에서는 canvas/panel 영역의 owned subtree만 canvas가 직접 초기화하고, container/frame 내부는 각 owner가 초기화한다.
- 이미 초기화가 끝난 canvas 아래에서 `Awake()`가 발생한 경우, 컴포넌트는 owner canvas를 찾아 즉시 `Init(...)` 된다.

## Reference

- Parent: `../00-overview/SKILL.md`
- UI Canvas System: [11-ui-canvas-system](../../10-base-system/11-ui-canvas-system/SKILL.md)
