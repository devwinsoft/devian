# 10-ui-component-base

Status: ACTIVE
AppliesTo: v11

## Purpose

`UIComponentBase`의 초기화/파괴/pool reset 계약을 정의한다.
현재 구현 기준으로 `UIComponentBase`는 canvas lifecycle-aware component이지만,
init owner는 항상 가장 가까운 subtree owner다.

- canvas-owned component는 `UIBaseCanvas`가 초기화한다
- panel-owned component는 `UIBasePanel`이 초기화한다
- frame/container-owned component는 해당 owner가 초기화한다

## Scope

### Includes

- `UIComponentBase : MonoBehaviour`
- `public void Init(Canvas canvas)` 진입점
- `protected virtual void onInit(Canvas canvas)` 확장점
- `Awake()`에서 `GetComponentInParent<UIBaseCanvas>()` 탐색 후 owner canvas가 이미 initialized 상태면 즉시 `Init(...)`
- pool despawn 시 `_ResetForPool()`을 통해 init state reset
- `UIBaseCanvas.Init()` / `UIBasePanel.CreateFrame()` / `UIBasePanel.CreateContainer()` / pooled respawn 경로와의 연동

### Excludes

- `UIComponentCircleFilter`
- `UIComponentNonDrawing`

두 타입은 기존 부모 타입을 유지한다. 공통 base로 올리지 않는다.

## SSOT

### Implementation Location (3-path mirror)

| 경로 | 역할 |
|------|------|
| `framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Component/UIComponentBase.cs` | UPM mirror |
| `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/UIPackage/Runtime/Component/UIComponentBase.cs` | Packages mirror |
| `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/UIPackage/Runtime/Component/UIComponentBase.cs` | 현재 workspace 구현 기준 |

### API

```csharp
namespace Devian
{
    public abstract class UIComponentBase : MonoBehaviour
    {
        public bool isInitialized { get; }
        public Canvas canvas { get; }

        public void Init(Canvas canvas);

        internal void _ResetForPool();

        protected virtual void onAwake();
        protected virtual void onInit(Canvas canvas);
        protected virtual void onPoolDespawned();
        protected virtual void onDestroy();
    }
}
```

## Lifecycle

### Init Order

`UIComponentBase`는 항상 subtree owner보다 먼저 초기화된다.

```text
UIComponentBase.Init(canvas)
-> UIBaseFrame.onInit()
-> UIBaseContainer.onInit()
-> UIBasePanel.onInit(...)
-> UIBaseCanvas.onInit()
```

실제 호출 owner는 배치 위치에 따라 달라진다.

- canvas root 직하위 owned subtree: `UIBaseInitHelper.InitCanvasOwnedSubtree()`
- panel subtree: `UIBasePanel._InitFromCanvas()`
- container subtree: `UIBaseContainer._Init()`
- frame subtree: `UIBaseFrame._Init()`

### Dynamic Trees

- 동적 `CreateFrame()` 경로에서는 root frame subtree의 `UIComponentBase`가 frame보다 먼저 초기화된다
- 동적 `CreateContainer()` 경로에서는 container subtree의 `UIComponentBase`와 owned frame이 container보다 먼저 초기화된다
- 정적 `UIBaseCanvas.Init()` 경로에서는 canvas가 panel만 직접 호출하고, container/frame 내부는 각 owner가 초기화한다
- 이미 initialized 된 canvas 아래에서 `Awake()`가 발생하면, component는 owner canvas를 찾아 즉시 `Init(...)` 된다

### Pool Contract

- `UIBaseCanvas` / `UIBasePanel`은 init once지만 `UIComponentBase`는 pooled respawn 시 재초기화 대상이다
- base `_ResetForPool()`는 `onPoolDespawned()`를 호출한 뒤 `isInitialized = false`, `canvas = null`로 되돌린다
- 따라서 pooled respawn 뒤 다음 `Init(canvas)` 경로에서 `onInit(Canvas)`가 다시 호출된다
- event subscription이나 runtime cache cleanup이 필요한 component는 `onPoolDespawned()`에서 정리해야 한다

## Reference

- Parent: `../00-overview/SKILL.md`
- UI Canvas System: [11-ui-canvas-system](../../10-base-system/11-ui-canvas-system/SKILL.md)
