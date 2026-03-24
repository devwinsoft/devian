# UI Canvas System

Status: ACTIVE
AppliesTo: v11

---

## Overview

### Purpose

Unity UI를 위한 UIBaseCanvas / UIBasePanel / UIBaseContainer 기본 구조를 제공한다.
Canvas owner, UI 기능 단위(Frame), Container, UIComponentBase의 초기화 수명주기를 표준화한다.

### Terms

| Term | Definition |
|------|------------|
| **UIBaseCanvas** | 비제네릭 canvas owner base. Canvas 캐시, Init 흐름, Validate, dynamic container registration을 담당한다 |
| **UIBaseCanvas\<TCanvas\>** | 타입 안전 singleton layer. `static Instance`를 제공하며 `where TCanvas : UIBaseCanvas` 제약을 가진다 |
| **UIBasePanel** | Canvas 하위 UI 기능 단위의 비제네릭 기반 클래스. _InitFromCanvas(MonoBehaviour) 진입점 제공 |
| **UIBasePanel\<TCanvas\>** | 타입 안전 버전. 강타입 owner 참조 + onInit(TCanvas) 확장점 제공 |
| **UIComponentBase** | Canvas lifecycle-aware UI component base. `Init(Canvas)`와 `onInit(Canvas)` 확장점을 제공한다 |
| **UIBaseContainer** | Container 기반 클래스. UIBaseCanvas.Init()에서 자동 수집되며 component → frame → container 순서 규약을 따른다 |
| **UIBaseFrame** | Container 내부 하위 요소의 공통 기반 클래스. scroll 상태는 소유하지 않으며, scroll 전용 계약은 별도 인터페이스로 분리 |

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
| **MUST** | UIBaseCanvas.Awake()는 non-virtual. Instance 설정 + canvas 캐시 + onAwake() |
| **MUST** | UIBaseCanvas / UIBasePanel / UIBaseContainer / UIBaseFrame의 `OnDestroy()`는 non-virtual |
| **MUST** | destroy 정리 로직은 `onDestroy()`로만 override |
| **MUST** | `onDestroy()`는 `Application.isPlaying && !BaseApplication.IsShuttingDown && !BaseApplication.IsApplicationQuitting`일 때만 호출 |
| **MUST** | UIBasePanel.Awake()는 non-virtual |
| **MUST** | panel 표시 전이는 `Show()` / `Hide()`로 수행하고 `onShow()` / `onHide()`를 override한다 |
| **MUST** | `UIComponentBase`는 `Awake()`에서 owner `UIBaseCanvas`가 이미 초기화된 경우 즉시 `Init(canvas)` 된다 |
| **MUST** | `_InitFromCanvas()` / `_Init()` 중복 호출 방지 (initialized 가드) |
| **MUST** | `UIBaseContainer._InitComplete()`는 idempotent 해야 한다 |
| **MUST** | Init 우선순위: UIComponentBase.onInit → UIBaseFrame.onInit → UIBaseContainer.onInit → UIBaseCanvas.onInit |
| **MUST** | `UIComponentCircleFilter` / `UIComponentNonDrawing`는 기존 부모 타입을 유지한다 |
| **MUST** | 동적 container subtree는 owner canvas가 `RegisterDynamicContainerTree()`로 편입한다 |
| **MUST** | 동적 frame subtree는 owner canvas가 `RegisterDynamicFrameTree()`로 편입한다 |

### Prohibited Actions

| Action | Reason |
|--------|--------|
| UIBasePanel.`Awake()`를 `virtual`로 선언 | 수명주기 순서 보장 불가 |
| UIBaseCanvas.Awake()/OnDestroy() override | non-virtual — onAwake()/onDestroy() 사용 |
| UIBasePanel / UIBaseContainer / UIBaseFrame의 `OnDestroy()` 직접 override | non-virtual hook 규약 위반. `onDestroy()` 사용 |
| UIBasePanel 가시성을 `SetActive()`만으로 제어 | `Show()` / `Hide()` 상태 훅 우회 |
| `BaseUIFrame` 이름 사용 | `UIBasePanel`로 변경됨 |
| `InspectorPoolFactory` 사용 | `BundlePool` 전용 |

---

## SSOT

### Canonical Code Path

```
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Base/
├── UIBaseCanvas.cs
├── UIBasePanel.cs
├── UIManager.cs
├── UIMessageSystem.cs
├── UIBaseContainer.cs
└── UIBaseFrame.cs
```

### File List

| File | Purpose |
|------|---------|
| `UIBaseCanvas.cs` | UIBaseCanvas (비제네릭 base) + UIBaseCanvas\<T\> + BillboardMode + UICanvasInitPhase |
| `UIBasePanel.cs` | UIBasePanel + UIBasePanel\<TCanvas\> 클래스 |
| `UIBaseContainer.cs` | UIBaseContainer 추상 클래스 |
| `UIBaseFrame.cs` | UIBaseFrame 추상 클래스 (Container 내부 하위 요소) |
| `UIBaseInitHelper.cs` | canvas/container/frame owned subtree 초기화 helper |
| `UIUtils.cs` | 공용 static 유틸리티 (좌표 변환, Billboard, Cursor) — `50-ui-utils` 참조 |

### Public API Signatures

#### UIBaseContainer (신규)

```csharp
namespace Devian
{
    public abstract class UIBaseContainer : MonoBehaviour, IPoolable
    {
        public bool isContainerInitialized { get; }
        public RectTransform rectTransform { get; }
        public Canvas canvas { get; }

        // Internal (UIBaseCanvas가 호출)
        internal void _Init(Canvas canvas);
        internal void _InitComplete();

        // Override points
        protected virtual void onAwake();
        protected virtual void onInit();
        protected virtual void onInitComplete();
        protected virtual void onDestroy();

        // IPoolable
        public virtual void OnPoolSpawned();
        public virtual void OnPoolDespawned();
    }
}
```

#### UIBaseFrame

```csharp
namespace Devian
{
    public abstract class UIBaseFrame : MonoBehaviour
    {
        public bool isFrameInitialized { get; }
        public bool isFrameInitComplete { get; }
        public RectTransform rectTransform { get; }
        public Canvas canvas { get; }

        // Internal (UIBaseContainer가 호출)
        internal void _Init(Canvas canvas);
        internal void _InitComplete();

        // Size (virtual — Grid 등 동적 크기 하위 클래스에서 override)
        public virtual float GetWidth();
        public virtual float GetHeight();

        // Scroll 전용 계약은 IUIScrollSection 인터페이스로 분리됨
        // (Show/Hide/Refresh 없음 — scroll 정보를 소유하지 않음)

        internal virtual void _Clear();

        // Override points
        protected virtual void onAwake();
        protected virtual void onInit();
        protected virtual void onInitComplete();
        protected virtual void onDestroy();
    }
}
```

#### UIBaseCanvas (비제네릭 base)

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

        internal void RegisterDynamicContainerTree(UIBaseContainer root);
        internal void RegisterDynamicFrameTree(UIBaseFrame root);

        protected virtual void onAwake();
        protected virtual void onDestroy();
        protected virtual void onInit();
        protected virtual void onInitComplete();
    }
}
```

#### UIBaseCanvas\<TCanvas\>

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

#### UIBasePanel (비제네릭)

```csharp
namespace Devian
{
    public abstract class UIBasePanel : MonoBehaviour
    {
        public bool isInitialized { get; }
        public bool isShown { get; }
        public RectTransform rectTransform { get; }
        protected MonoBehaviour ownerBase { get; }
        protected UIBaseCanvas ownerCanvas { get; }

        protected void Awake();
        protected virtual void onAwake();
        public void Show();
        public void Hide();
        protected virtual void onDestroy();

        internal void _InitFromCanvas(MonoBehaviour owner);
        internal void _InitComplete();

        protected abstract void onInitFromCanvas(MonoBehaviour owner);
        protected virtual void onInitComplete();
        protected virtual void onShow();
        protected virtual void onHide();

        public T CreateContainer<T>(string prefabName, Transform parent = null)
            where T : UIBaseContainer;

        public T CreateFrame<T>(string prefabName, Transform parent = null)
            where T : UIBaseFrame;
    }
}
```

### Panel Visibility Sequence

```text
panel.Show()
  -> gameObject.SetActive(true) if needed
  -> onShow()

panel.Hide()
  -> onHide()
  -> default onHide(): gameObject.SetActive(false)
```

기본 `onHide()`는 즉시 비활성화다. 지연 hide나 transition이 필요하면 override에서 deactivate 시점을 직접 제어한다.

#### UIBasePanel\<TCanvas\>

```csharp
namespace Devian
{
    public abstract class UIBasePanel<TCanvas> : UIBasePanel
        where TCanvas : UIBaseCanvas
    {
        public TCanvas owner { get; }

        protected sealed override void onInitFromCanvas(MonoBehaviour owner);
        protected virtual void onInit(TCanvas owner);
    }
}
```

### Initialization Sequence (Canonical)

```
UIBaseCanvas.Init()
├── 1. if (isInitialized) return
├── 2. isInitialized = true
├── 3. mContainers = GetComponentsInChildren<UIBaseContainer>(true)
├── 4. mPanels = GetComponentsInChildren<UIBasePanel>(true)
│
├── Phase 1: Canvas-owned Component / Frame Init
│   └── UIBaseInitHelper.InitOwnedSubtree(canvas.transform, canvas)
│       ├── canvas/panel 영역의 UIComponentBase.Init(canvas)
│       └── container 밖 root frame가 있으면 frame._Init(canvas)
│
├── Phase 2: Container Init
│   └── foreach container: container._Init(canvas)
│       └── subtree rule: owned component → owned frame → container
│
├── Phase 3: Panel Init
│   └── foreach panel: panel._InitFromCanvas(this)  → onInit(TCanvas)
│
├── Phase 4: Canvas Init
│   └── onInit()
│
├── Phase 5: Container InitComplete
│   ├── foreach container: container._InitComplete()
│   └── FlushPendingDynamicContainers()
│
├── Phase 6: Panel InitComplete
│   └── foreach panel: panel._InitComplete()
│
├── Phase 7: Canvas InitComplete
│   └── onInitComplete()
│
└── Phase 8: Notify
    └── UIManager.messageSystem.Notify(UI_MESSAGE.InitOnce)
```

### Dynamic Container Registration

`UIBasePanel.CreateContainer<T>()`는 `BundlePool.Spawn<T>()` 후
`ownerCanvas.RegisterDynamicContainerTree(root)`에 위임한다.

`RegisterDynamicContainerTree()`의 책임:

- root 이하 `UIBaseContainer` subtree 수집
- 각 container는 `_Init(canvas)` 내부에서 자신의 subtree `UIComponentBase`와 `UIBaseFrame`를 먼저 초기화
- `canvas.Init()` 완료 전이면 pending queue 등록
- `canvas.Init()` 완료 후면 `_InitComplete()` 즉시 호출

이 구조로 `panel.onInit()` 내부와 일반 런타임 시점 모두 지원한다.

### Dynamic Frame Registration

`UIBasePanel.CreateFrame<T>()`는 `BundlePool.Spawn<T>()` 후
`ownerCanvas.RegisterDynamicFrameTree(root)`에 위임한다.

`RegisterDynamicFrameTree()`의 책임:

- root 이하 `UIBaseFrame` subtree 수집
- root frame는 `_Init(canvas)` 내부에서 자신의 owned subtree `UIComponentBase`를 먼저 초기화
- `PanelInitComplete` 이전이면 pending queue 등록
- `PanelInitComplete` 이후 또는 `canvas.Init()` 완료 후면 `_InitComplete()` 즉시 호출

이 구조로 `panel.onInit()` 내부에서 동적으로 생성된 frame도
`panel._InitComplete()` 이전에 안정적으로 init complete 상태에 도달한다.

### Singleton Behavior

| 항목 | 설명 |
|------|------|
| **상속** | `MonoBehaviour` 직접 상속 |
| **Instance** | `static TCanvas Instance` — Awake에서 설정, OnDestroy에서 클린업 |
| **DontDestroyOnLoad** | 미적용 — 씬 전환 시 자동 파괴 |

### Type Constraints

| Generic | Constraint |
|---------|------------|
| `UIBaseCanvas<TCanvas>` | `where TCanvas : UIBaseCanvas` |
| `UIBasePanel<TCanvas>` | `where TCanvas : UIBaseCanvas` |

---

## DoD (Definition of Done) Checklist

### Files Exist
- [ ] `Base/UIBaseCanvas.cs`
- [ ] `Base/UIBasePanel.cs`
- [ ] `UIBaseContainer.cs` (신규)
- [ ] `UIBaseCanvas` 비제네릭 base + `UIBaseCanvas<TCanvas>` 제네릭 layer가 같은 파일에 존재

### Naming
- [ ] `BaseUIFrame` 문자열이 코드에 0건
- [ ] 모든 타입이 `namespace Devian { }` 내에 선언됨
- [ ] internal 메서드가 `_` 접두어 (`_Init`, `_InitFromCanvas`, `_InitComplete`)
- [ ] protected 메서드가 lowerCamelCase (`onInit`, `onInitComplete`)

### Init Order
- [ ] UIComponentBase.onInit() → UIBaseFrame.onInit() → UIBaseContainer.onInit() → UIBaseCanvas.onInit()
- [ ] 중복 _Init() 방지 (isContainerInitialized 가드)
- [ ] 중복 _InitComplete() 방지 (idempotent guard)
- [ ] 중복 _InitFromCanvas() 방지 (isInitialized 가드)
- [ ] 동적 container subtree가 owner canvas lifecycle에 편입됨

### Build
- [ ] 컴파일 오류 0개

---

## Reference

- **Singleton**: [20-common-package/29-singleton/SKILL.md](../../../20-common-package/29-singleton/SKILL.md)
- **Pool System**: [20-common-package/27-pool-system/SKILL.md](../../../20-common-package/27-pool-system/SKILL.md)
- **UIManager**: [10-ui-manager/SKILL.md](../10-ui-manager/SKILL.md)
- **UIComponentBase**: [10-ui-component-base/SKILL.md](../../20-ui-components/10-ui-component-base/SKILL.md)
- **UIScrollContainer / IUIScrollSection**: [10-ui-scroll-container/SKILL.md](../../21-ui-scroll-system/10-ui-scroll-container/SKILL.md)
