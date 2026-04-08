# UI Canvas System

Status: ACTIVE
AppliesTo: v11

---

## Overview

### Purpose

Unity UI를 위한 `UIBaseCanvas` / `UIBasePanel` / `UIBaseContainer` / `UIBaseFrame` /
`UIComponentBase` 기본 수명주기 계약을 정의한다.

현재 구현 기준 핵심은 다음 두 가지다.

- `UIBaseCanvas`는 panel만 직접 관리한다.
- `UIBasePanel`이 자기 subtree의 `UIBaseContainer` / panel-owned `UIBaseFrame`를 관리한다.

### Terms

| Term | Definition |
|------|------------|
| **UIBaseCanvas** | 비제네릭 canvas owner base. Canvas 캐시, init once 흐름, validate, pool spawn/despawn bridge를 담당한다 |
| **UIBaseCanvas\<TCanvas\>** | 타입 안전 singleton layer. `static Instance`를 제공한다 |
| **UIBasePanel** | canvas 하위 UI 기능 단위의 비제네릭 base. panel-owned subtree init과 dynamic container/frame registration을 담당한다 |
| **UIBasePanel\<TCanvas\>** | 강타입 owner canvas 참조를 제공하는 typed panel layer |
| **UIComponentBase** | canvas lifecycle-aware component base. `Init(Canvas)`로 초기화되며 pooled respawn 시 재초기화될 수 있다 |
| **UIBaseContainer** | panel이 소유하는 subtree owner. 자기 하위 component / frame subtree를 먼저 초기화한 뒤 `onInit()`를 받는다 |
| **UIBaseFrame** | panel 또는 container가 소유하는 frame base. 자기 하위 component / frame subtree를 먼저 초기화한 뒤 `onInit()`를 받는다 |

---

## Init Semantics

`onInit()` / `onInitComplete()`의 기준은 `Awake()`가 아니라 **owner lifecycle**이다.

- `Awake()`는 ref cache와 lightweight setup만 담당한다.
- semantic init boundary는 `UIBaseCanvas.Init()`에서 시작되는 `_Init()` / `Init(canvas)` 호출이다.
- 따라서 `onInit()`은 "owner canvas/panel/container/frame에 lifecycle 편입되었다"는 뜻이지, "Unity가 방금 생성했다"는 뜻이 아니다.
- owner가 이미 initialized 상태라면 동적 생성/attach/respawn 경로에서 `_Init()`이 즉시 실행될 수 있다.
- owner가 이미 init complete 상태라면 `_InitComplete()`도 같은 call path 안에서 즉시 실행될 수 있다.
- 즉 `Awake()` 직후 `onInit()`이 보일 수는 있지만, 그 이유는 `Awake()` 때문이 아니라 **이미 initialized 된 owner tree에 편입됐기 때문**이다.

해석 규칙:

- `onInit()`
  - owner canvas 참조와 subtree lifecycle이 보장된 상태
  - child component/frame/container 초기화가 먼저 끝난 뒤 호출된다
- `onInitComplete()`
  - owner subtree의 init chain이 모두 끝난 뒤 호출된다
  - 이미 init complete 된 owner에 동적으로 붙으면 즉시 호출될 수 있다

금지 규칙:

- `Awake()`를 별도 UI init 시스템처럼 쓰지 않는다
- scroll/popup/toast 등 하위 시스템이 base와 별개 `Build/Setup/Init` 의미를 새로 만들지 않는다
- frame/container/component가 필요로 하는 init은 기존 `_Init()` / `_InitComplete()` 경로를 재사용한다

---

## Policy

### Namespace Policy

| Rule | Description |
|------|-------------|
| **MUST** | `namespace Devian` 단일 루트 네임스페이스만 사용 |

### Lifecycle Policy

| Rule | Description |
|------|-------------|
| **MUST** | `Awake()` → `onAwake()` 패턴 사용 |
| **MUST** | `UIBaseCanvas.Awake()`는 non-virtual. `Instance` 설정 + canvas 캐시 + `onAwake()` 호출 |
| **MUST** | `UIBasePanel.Awake()`는 non-virtual. cached `RectTransform` 설정 + `onAwake()` 호출 |
| **MUST** | `UIBaseCanvas` / `UIBasePanel` / `UIBaseContainer` / `UIBaseFrame`의 `OnDestroy()`는 non-virtual |
| **MUST** | destroy 정리 로직은 `onDestroy()`만 override |
| **MUST** | `onDestroy()`는 `Application.isPlaying && !BaseApplication.IsShuttingDown && !BaseApplication.IsApplicationQuitting`일 때만 호출 |
| **MUST** | panel 표시 전이는 `Show()` / `Hide()`로 수행하고 `onShow()` / `onHide()`를 override한다 |
| **MUST** | `UIBaseCanvas` / `UIBasePanel`은 인스턴스 lifetime 기준 `Init()` 1회 정책이다 |
| **MUST** | pooled `UIBaseCanvas` / `UIBasePanel`은 respawn 때 `Init()`를 다시 돌리지 않고 `onPoolSpawned()` / `onPoolDespawned()`로 runtime state만 복구한다 |
| **MUST** | `UIBaseContainer` / `UIBaseFrame` / `UIComponentBase`는 pool despawn 시 init state를 reset하고, respawn 후 다음 init 경로에서 `onInit()`이 다시 호출될 수 있어야 한다 |
| **MUST** | `UIComponentBase`는 `Awake()`에서 owner `UIBaseCanvas`가 이미 initialized 상태면 즉시 `Init(canvas)` 된다 |
| **MUST** | "즉시 init"은 `Awake()` 자체 규칙이 아니라 owner가 이미 initialized 상태인 경우의 결과로 해석한다 |
| **MUST** | `UIBaseCanvas`는 canvas-owned component와 panel만 직접 초기화한다. container / frame를 직접 수집하거나 관리하지 않는다 |
| **MUST** | `UIBasePanel`이 자기 subtree의 container / panel-owned frame lifecycle을 관리한다 |
| **MUST** | 동적 container subtree는 owner panel `CreateContainer<T>()` 경로로 lifecycle에 편입된다 |
| **MUST** | 동적 frame subtree는 owner panel `CreateFrame<T>()` 경로로 lifecycle에 편입된다 |
| **MUST** | 동적/pooled object를 initialized owner tree에 붙일 때는 새 init hook을 만들지 말고 `_Init()` / `_InitComplete()` 또는 `Init(canvas)`를 즉시 재사용한다 |
| **MUST** | Init 우선순위는 `UIComponentBase.onInit` → `UIBaseFrame.onInit` → `UIBaseContainer.onInit` → `UIBaseCanvas.onInit` 이다 |
| **MUST** | `UIBaseContainer._InitComplete()` / `UIBaseFrame._InitComplete()`는 idempotent 해야 한다 |
| **MUST** | `UIComponentCircleFilter` / `UIComponentNonDrawing`는 기존 부모 타입을 유지한다 |

### Prohibited Actions

| Action | Reason |
|--------|--------|
| `UIBasePanel.Awake()`를 `virtual`로 선언 | 수명주기 순서 보장 불가 |
| `UIBaseCanvas.Awake()` / `OnDestroy()` override | non-virtual hook 규약 위반. `onAwake()` / `onDestroy()` 사용 |
| `UIBasePanel` / `UIBaseContainer` / `UIBaseFrame`의 `OnDestroy()` 직접 override | non-virtual hook 규약 위반. `onDestroy()` 사용 |
| `UIBaseCanvas`가 container / frame subtree를 직접 수집·등록하도록 설계 | 현재 구현과 불일치. 해당 책임은 panel에 있음 |
| panel 가시성을 `SetActive()`만으로 제어 | `Show()` / `Hide()` 상태 훅 우회 |
| `BaseUIFrame` 이름 사용 | `UIBasePanel`로 변경됨 |
| `InspectorPoolFactory` 사용 | `BundlePool` 전용 |

---

## SSOT

### Implementation Location (3-path mirror)

| 경로 | 역할 |
|------|------|
| `framework-cs/upm/com.devian.foundation/Samples~/UIPackage/` | UPM mirror |
| `framework-cs/apps/UnityExample/Packages/com.devian.foundation/Samples~/UIPackage/` | Packages mirror |
| `framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/{version}/UIPackage/` | 현재 workspace 구현 기준 |

### Canonical Files

```text
framework-cs/apps/UnityExample/Assets/Samples/Devian Foundation/0.1.0/UIPackage/Runtime/Base/
├── UIBaseCanvas.cs
├── UIBasePanel.cs
├── UIBaseContainer.cs
├── UIBaseFrame.cs
├── UIBaseInitHelper.cs
├── UIManager.cs
└── UIMessageSystem.cs
```

### File List

| File | Purpose |
|------|---------|
| `UIBaseCanvas.cs` | `UIBaseCanvas` + `UIBaseCanvas<TCanvas>` + `BillboardMode` + `UICanvasInitPhase` |
| `UIBasePanel.cs` | `UIBasePanel` + `UIBasePanel<TCanvas>` |
| `UIBaseContainer.cs` | panel-owned container base + pool reset contract |
| `UIBaseFrame.cs` | panel/container-owned frame base + pool reset contract |
| `UIBaseInitHelper.cs` | canvas / panel / container / frame owned subtree init + reset helper |

---

## Public API

### UIBaseContainer

```csharp
namespace Devian
{
    public abstract class UIBaseContainer : MonoBehaviour, IPoolable
    {
        public bool isContainerInitialized { get; }
        public RectTransform rectTransform { get; }
        public Canvas canvas { get; }

        internal void _Init(Canvas canvas);
        internal void _InitComplete();
        internal void _ResetForPool();

        protected virtual void onAwake();
        protected virtual void onInit();
        protected virtual void onInitComplete();
        protected virtual void onPoolSpawned();
        protected virtual void onPoolDespawned();
        protected virtual void onDestroy();

        public void OnPoolSpawned();
        public void OnPoolDespawned();
    }
}
```

### UIBaseFrame

```csharp
namespace Devian
{
    public abstract class UIBaseFrame : MonoBehaviour
    {
        public bool isFrameInitialized { get; }
        public bool isFrameInitComplete { get; }
        public RectTransform rectTransform { get; }
        public Canvas canvas { get; }

        internal void _Init(Canvas canvas);
        internal void _InitComplete();
        internal void _ResetForPool();

        protected void _HandlePoolSpawned();
        protected void _HandlePoolDespawned();

        public virtual float GetWidth();
        public virtual float GetHeight();

        internal virtual void _Clear();

        protected virtual void onAwake();
        protected virtual void onInit();
        protected virtual void onInitComplete();
        protected virtual void onPoolSpawned();
        protected virtual void onPoolDespawned();
        protected virtual void onDestroy();
    }
}
```

`UIBaseFrame` 자체는 `IPoolable`을 구현하지 않는다.
poolable frame subclass가 public `OnPoolSpawned()` / `OnPoolDespawned()`를 노출할 때
반드시 base `_HandlePoolSpawned()` / `_HandlePoolDespawned()`로 연결한다.

동적으로 child subtree를 만들 때는 새 init 시스템을 만들지 않는다.
`UIBaseFrame.InitDynamicSubtree(root)`로 기존 `UIBaseInitHelper.InitOwnedSubtree(...)`
와 `_InitComplete()`를 재사용해 현재 frame lifecycle에 편입한다.

### UIBaseCanvas

```csharp
namespace Devian
{
    public abstract class UIBaseCanvas : MonoBehaviour
    {
        public Canvas canvas { get; }
        public bool isInitialized { get; }
        public bool isInitComplete { get; }

        public void Init();
        public virtual bool Validate(out string reason);

        protected void _CacheCanvas();
        protected void _HandlePoolSpawned();
        protected void _HandlePoolDespawned();

        protected virtual void onAwake();
        protected virtual void onDestroy();
        protected virtual void onInit();
        protected virtual void onInitComplete();
        protected virtual void onPoolSpawned();
        protected virtual void onPoolDespawned();
    }
}
```

### UIBaseCanvas\<TCanvas\>

```csharp
namespace Devian
{
    public abstract class UIBaseCanvas<TCanvas> : UIBaseCanvas
        where TCanvas : UIBaseCanvas
    {
        public static TCanvas Instance { get; }
    }
}
```

### UIBasePanel

```csharp
namespace Devian
{
    public abstract class UIBasePanel : MonoBehaviour
    {
        public bool isInitialized { get; }
        public bool isShown { get; }
        public RectTransform rectTransform { get; }
        protected MonoBehaviour ownerBase { get; }

        public void Show();
        public void Hide();

        internal void _InitFromCanvas(MonoBehaviour owner);
        internal void _InitComplete();
        internal void _HandleOwnerPoolSpawned();
        internal void _HandleOwnerPoolDespawned();

        protected virtual void onAwake();
        protected abstract void onInitFromCanvas(MonoBehaviour owner);
        protected virtual void onInitComplete();
        protected virtual void onPoolSpawned();
        protected virtual void onPoolDespawned();
        protected virtual void onShow();
        protected virtual void onHide();
        protected virtual void onDestroy();

        public T CreateContainer<T>(string prefabName, Transform parent = null)
            where T : UIBaseContainer;

        public T CreateFrame<T>(string prefabName, Transform parent = null)
            where T : UIBaseFrame;
    }
}
```

### UIBasePanel\<TCanvas\>

```csharp
namespace Devian
{
    public abstract class UIBasePanel<TCanvas> : UIBasePanel
        where TCanvas : UIBaseCanvas
    {
        protected TCanvas ownerCanvas { get; }

        protected sealed override void onInitFromCanvas(MonoBehaviour owner);
        protected virtual void onInit(TCanvas canvas);
    }
}
```

---

## Initialization Sequence

### Canvas Init (Canonical)

```text
UIBaseCanvas.Init()
├── 1. if (isInitialized) return
├── 2. isInitialized = true
├── 3. mPanels = GetComponentsInChildren<UIBasePanel>(true)
│
├── Phase 1: Canvas-owned Component Init
│   └── UIBaseInitHelper.InitCanvasOwnedSubtree(canvas.transform, canvas)
│       └── canvas 직속 영역의 UIComponentBase.Init(canvas)
│
├── Phase 2: Panel Init
│   └── foreach panel: panel._InitFromCanvas(this)
│       ├── UIBaseInitHelper.InitPanelOwnedSubtree(panelRoot, canvas, ownedFrames)
│       │   ├── panel-owned UIComponentBase.Init(canvas)
│       │   └── panel-owned root UIBaseFrame._Init(canvas)
│       ├── UIBaseInitHelper.CollectOwnedContainers(panelRoot, containers)
│       └── foreach container: container._Init(canvas)
│           └── subtree rule: owned component → owned frame → container
│
├── Phase 3: Canvas Init
│   └── onInit()
│
├── Phase 4: Panel InitComplete
│   └── foreach panel: panel._InitComplete()
│       ├── foreach owned container: container._InitComplete()
│       └── foreach panel-owned root frame: frame._InitComplete()
│
├── Phase 5: Canvas InitComplete
│   └── onInitComplete()
│
└── Phase 6: Notify
    └── UIManager.messageSystem.Notify(UI_MESSAGE.InitOnce)
```

### Dynamic Container Registration

`UIBasePanel.CreateContainer<T>()`는 `BundlePool.Spawn<T>()` 후
owner panel 내부 `RegisterDynamicContainerTree(root)`로 편입한다.

동작:

- root 이하 `UIBaseContainer`를 panel 기준 subtree 규칙으로 수집
- 각 container가 아직 init 전이면 resolved owner canvas로 `container._Init(canvas)` 실행
- panel init complete 이전이면 `_containers`에 임시 보관
- panel init complete 이후면 `container._InitComplete()` 즉시 실행

즉, dynamic container lifecycle owner는 canvas가 아니라 panel이다.

### Dynamic Frame Registration

`UIBasePanel.CreateFrame<T>()`는 frame instance 생성 후
owner panel 내부 `RegisterDynamicFrameTree(root)`로 편입한다.

동작:

- root frame가 아직 init 전이면 `root._Init(ownerCanvas.canvas)` 실행
- root frame가 아직 init 전이면 resolved owner canvas로 `root._Init(canvas)` 실행
- panel init complete 이전이면 `_ownedFrames`에 임시 보관
- panel init complete 이후면 `root._InitComplete()` 즉시 실행

이 경로도 canvas registry가 아니라 panel registry다.

핵심:

- dynamic frame의 `onInit()`은 "방금 Instantiate/Spawn 됐다"가 아니라 "이미 살아 있는 owner panel tree에 편입됐다"는 의미다.
- 따라서 spawn 직후 `onInit()`이 호출되어도 그건 `Awake()` 규칙이 아니라 panel registration 규칙이다.

---

## Pool Contract

### Canvas / Panel

- `UIBaseCanvas` / `UIBasePanel`은 init once 정책이다
- pooled canvas subclass는 public `OnPoolSpawned()` / `OnPoolDespawned()`에서
  base `_HandlePoolSpawned()` / `_HandlePoolDespawned()`를 호출해야 한다
- spawn 시 canvas-owned component 재초기화와 panel-owned subtree 재초기화는 base helper가 수행한다
- despawn 시 panel-owned subtree와 canvas-owned component reset은 base helper가 수행한다

### Container / Frame / Component

- pooled `UIBaseContainer` / `UIBaseFrame` / `UIComponentBase`는 despawn 시 init state를 reset한다
- 따라서 respawn 뒤 다음 `_Init()` / `Init(canvas)` 경로에서 `onInit()`이 다시 호출된다
- `UIBaseFrame`는 base class 자체가 `IPoolable`이 아니므로 subclass가 bridge를 제공해야 한다
- respawn 뒤 owner가 이미 initialized + init complete 상태라면 `_Init()`와 `_InitComplete()`가 같은 attach 경로에서 즉시 연속 호출될 수 있다

---

## Panel Visibility Sequence

```text
panel.Show()
  -> gameObject.SetActive(true) if needed
  -> onShow()

panel.Hide()
  -> onHide()
  -> default onHide(): gameObject.SetActive(false)
```

기본 `onHide()`는 즉시 비활성화다.
지연 hide나 transition이 필요하면 override에서 deactivate 시점을 직접 제어한다.

---

## Type Constraints

| Generic | Constraint |
|---------|------------|
| `UIBaseCanvas<TCanvas>` | `where TCanvas : UIBaseCanvas` |
| `UIBasePanel<TCanvas>` | `where TCanvas : UIBaseCanvas` |

---

## DoD

- [ ] `UIBaseCanvas`가 panel만 직접 관리하고 container / frame 직접 registry를 갖지 않는다
- [ ] `UIBasePanel`이 dynamic `CreateContainer()` / `CreateFrame()` lifecycle을 관리한다
- [ ] init order가 `UIComponentBase` → `UIBaseFrame` → `UIBaseContainer` → `UIBaseCanvas`를 유지한다
- [ ] `UIBaseCanvas` / `UIBasePanel`은 init once, `UIBaseContainer` / `UIBaseFrame` / `UIComponentBase`는 respawn re-init 계약을 유지한다
- [ ] poolable frame subclass가 base `_HandlePoolSpawned()` / `_HandlePoolDespawned()` bridge를 제공한다
- [ ] 컴파일 오류 0개

---

## Reference

- **Singleton**: [20-common-package/29-singleton/SKILL.md](../../../20-common-package/29-singleton/SKILL.md)
- **Pool System**: [20-common-package/27-pool-system/SKILL.md](../../../20-common-package/27-pool-system/SKILL.md)
- **UIManager**: [10-ui-manager/SKILL.md](../10-ui-manager/SKILL.md)
- **UIComponentBase**: [10-ui-component-base/SKILL.md](../../20-ui-components/10-ui-component-base/SKILL.md)
- **UIScrollContainer / IUIScrollSection**: [10-ui-scroll-container/SKILL.md](../../21-ui-scroll-system/10-ui-scroll-container/SKILL.md)
